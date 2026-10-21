#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import "GLPermissionGroups.h"

#if GL_GAME_CENTER
#import <GameKit/GameKit.h>
#endif

// Completion callback into managed code. `result` is a tagged string: "K<payload>" on success or
// "E<reason>|<message>" on failure. Always invoked on the main queue.
typedef void (*GLCloudSaveCallback)(int requestId, const char *result);

static void GLReply(GLCloudSaveCallback callback, int requestId, NSString *reply)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (callback != NULL) callback(requestId, [reply UTF8String]);
    });
}

#if GL_GAME_CENTER

// GKErrorICloudUnavailable, spelled as its value because the constant is annotated with a newer availability than its introduction.
static const NSInteger GLErrorICloudUnavailable = 15;

static NSString *GLReasonForError(NSError *error)
{
    if ([error.domain isEqualToString:GKErrorDomain])
    {
        if (error.code == GKErrorNotAuthenticated) return @"NotAuthenticated";
        if (error.code == GLErrorICloudUnavailable) return @"CloudUnavailable";
    }
    return @"Io";
}

static void GLReplyError(GLCloudSaveCallback callback, int requestId, NSError *error)
{
    GLReply(callback, requestId, [NSString stringWithFormat:@"E%@|%@", GLReasonForError(error), error.localizedDescription ?: @"Saved game request failed."]);
}

static void GLReplyJson(GLCloudSaveCallback callback, int requestId, NSArray *items)
{
    NSData *json = [NSJSONSerialization dataWithJSONObject:@{ @"items": items } options:0 error:nil];
    GLReply(callback, requestId, [@"K" stringByAppendingString:[[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding]]);
}

static NSDictionary *GLEntryForSave(GKSavedGame *save, NSString *base64)
{
    NSMutableDictionary *entry = [@{
        @"name": save.name ?: @"",
        @"description": @"",
        @"modified": @([save.modificationDate timeIntervalSince1970]),
        @"device": save.deviceName ?: @"",
    } mutableCopy];
    if (base64 != nil) entry[@"data"] = base64;
    return entry;
}

// Saved games exist only for the signed-in player, and only when the user has an iCloud account for the app.
int _GameLoversCloudSaveIsAvailable(void)
{
    return [NSFileManager defaultManager].ubiquityIdentityToken != nil ? 1 : 0;
}

void _GameLoversCloudSaveList(int requestId, GLCloudSaveCallback callback)
{
    [[GKLocalPlayer localPlayer] fetchSavedGamesWithCompletionHandler:^(NSArray<GKSavedGame *> * _Nullable savedGames, NSError * _Nullable error) {
        if (error != nil)
        {
            GLReplyError(callback, requestId, error);
            return;
        }

        // Conflicting versions share a name; the list shows each slot once, with its newest version.
        NSMutableDictionary<NSString *, GKSavedGame *> *newest = [NSMutableDictionary dictionary];
        for (GKSavedGame *save in savedGames)
        {
            GKSavedGame *existing = newest[save.name];
            if (existing == nil || [save.modificationDate compare:existing.modificationDate] == NSOrderedDescending) newest[save.name] = save;
        }

        NSMutableArray *items = [NSMutableArray array];
        for (GKSavedGame *save in newest.allValues) [items addObject:GLEntryForSave(save, nil)];
        GLReplyJson(callback, requestId, items);
    }];
}

// Returns every stored version of the slot with its bytes (base64): none, one, or several when devices conflicted.
void _GameLoversCloudSaveLoadVersions(const char *slot, int requestId, GLCloudSaveCallback callback)
{
    NSString *name = [NSString stringWithUTF8String:slot];
    [[GKLocalPlayer localPlayer] fetchSavedGamesWithCompletionHandler:^(NSArray<GKSavedGame *> * _Nullable savedGames, NSError * _Nullable error) {
        if (error != nil)
        {
            GLReplyError(callback, requestId, error);
            return;
        }

        NSMutableArray<GKSavedGame *> *matches = [NSMutableArray array];
        for (GKSavedGame *save in savedGames) if ([save.name isEqualToString:name]) [matches addObject:save];
        if (matches.count == 0)
        {
            GLReplyJson(callback, requestId, @[]);
            return;
        }

        NSMutableArray *items = [NSMutableArray array];
        dispatch_group_t group = dispatch_group_create();
        __block NSError *loadError = nil;
        for (GKSavedGame *save in matches)
        {
            dispatch_group_enter(group);
            [save loadDataWithCompletionHandler:^(NSData * _Nullable data, NSError * _Nullable dataError) {
                @synchronized (items)
                {
                    if (dataError != nil) loadError = dataError;
                    else [items addObject:GLEntryForSave(save, [data base64EncodedStringWithOptions:0])];
                }
                dispatch_group_leave(group);
            }];
        }

        dispatch_group_notify(group, dispatch_get_main_queue(), ^{
            if (loadError != nil) GLReplyError(callback, requestId, loadError);
            else GLReplyJson(callback, requestId, items);
        });
    }];
}

// Collapses conflicting versions of the slot to the chosen bytes.
void _GameLoversCloudSaveResolve(const char *slot, const char *base64, int requestId, GLCloudSaveCallback callback)
{
    NSString *name = [NSString stringWithUTF8String:slot];
    NSData *chosen = [[NSData alloc] initWithBase64EncodedString:[NSString stringWithUTF8String:base64] options:0];
    [[GKLocalPlayer localPlayer] fetchSavedGamesWithCompletionHandler:^(NSArray<GKSavedGame *> * _Nullable savedGames, NSError * _Nullable error) {
        if (error != nil)
        {
            GLReplyError(callback, requestId, error);
            return;
        }

        NSMutableArray<GKSavedGame *> *matches = [NSMutableArray array];
        for (GKSavedGame *save in savedGames) if ([save.name isEqualToString:name]) [matches addObject:save];
        [[GKLocalPlayer localPlayer] resolveConflictingSavedGames:matches withData:chosen completionHandler:^(NSArray<GKSavedGame *> * _Nullable resolved, NSError * _Nullable resolveError) {
            if (resolveError != nil) GLReplyError(callback, requestId, resolveError);
            else GLReply(callback, requestId, @"K");
        }];
    }];
}

// The save keeps running if the app is backgrounded: the background task is held natively until the OS confirms,
// because Unity's main loop is paused while backgrounded and could not end it.
void _GameLoversCloudSaveSave(const char *slot, const char *base64, int requestId, GLCloudSaveCallback callback)
{
    NSString *name = [NSString stringWithUTF8String:slot];
    NSData *data = [[NSData alloc] initWithBase64EncodedString:[NSString stringWithUTF8String:base64] options:0];

    __block UIBackgroundTaskIdentifier task = UIBackgroundTaskInvalid;
    void (^endTask)(void) = ^{
        if (task != UIBackgroundTaskInvalid)
        {
            [[UIApplication sharedApplication] endBackgroundTask:task];
            task = UIBackgroundTaskInvalid;
        }
    };

    dispatch_async(dispatch_get_main_queue(), ^{
        task = [[UIApplication sharedApplication] beginBackgroundTaskWithName:@"GameLovers.CloudSave" expirationHandler:endTask];
        [[GKLocalPlayer localPlayer] saveGameData:data withName:name completionHandler:^(GKSavedGame * _Nullable savedGame, NSError * _Nullable error) {
            dispatch_async(dispatch_get_main_queue(), endTask);
            if (error != nil) GLReplyError(callback, requestId, error);
            else GLReply(callback, requestId, @"K");
        }];
    });
}

void _GameLoversCloudSaveDelete(const char *slot, int requestId, GLCloudSaveCallback callback)
{
    NSString *name = [NSString stringWithUTF8String:slot];
    [[GKLocalPlayer localPlayer] deleteSavedGamesWithName:name completionHandler:^(NSError * _Nullable error) {
        if (error != nil) GLReplyError(callback, requestId, error);
        else GLReply(callback, requestId, @"K");
    }];
}

#else

// Cloud saves ride on Game Center: without the capability in the Mobile Services Config the bridge links no GameKit symbols.
static void GLCloudSaveDisabled(GLCloudSaveCallback callback, int requestId)
{
    GLReply(callback, requestId, @"ECloudUnavailable|Game Center is not enabled for this build. Turn on the Game Center capability in the Mobile Services Config.");
}

int _GameLoversCloudSaveIsAvailable(void) { return 0; }
void _GameLoversCloudSaveList(int requestId, GLCloudSaveCallback callback) { GLCloudSaveDisabled(callback, requestId); }
void _GameLoversCloudSaveLoadVersions(const char *slot, int requestId, GLCloudSaveCallback callback) { GLCloudSaveDisabled(callback, requestId); }
void _GameLoversCloudSaveResolve(const char *slot, const char *base64, int requestId, GLCloudSaveCallback callback) { GLCloudSaveDisabled(callback, requestId); }
void _GameLoversCloudSaveSave(const char *slot, const char *base64, int requestId, GLCloudSaveCallback callback) { GLCloudSaveDisabled(callback, requestId); }
void _GameLoversCloudSaveDelete(const char *slot, int requestId, GLCloudSaveCallback callback) { GLCloudSaveDisabled(callback, requestId); }

#endif
