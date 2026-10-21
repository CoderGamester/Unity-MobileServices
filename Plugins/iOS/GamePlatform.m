#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import "GLPermissionGroups.h"

#if GL_GAME_CENTER
#import <GameKit/GameKit.h>
#endif

extern UIViewController *UnityGetGLViewController(void);

// Completion callback into managed code. `result` is NULL on failure. Always invoked on the main queue.
typedef void (*GLGamePlatformCallback)(int requestId, const char *result);

static void GLComplete(GLGamePlatformCallback callback, int requestId, NSString *result)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (callback != NULL) callback(requestId, result != nil ? [result UTF8String] : NULL);
    });
}

#if GL_GAME_CENTER

#pragma mark - Authentication

static int gAuthRequestId = 0;
static GLGamePlatformCallback gAuthCallback = NULL;

static NSString *GLPlayerPayload(GKLocalPlayer *player)
{
    return [NSString stringWithFormat:@"%@\n%@", player.gamePlayerID, player.displayName ?: @""];
}

static void GLFinishAuthentication(NSString *payload)
{
    if (gAuthCallback == NULL) return;

    GLGamePlatformCallback callback = gAuthCallback;
    int requestId = gAuthRequestId;
    gAuthCallback = NULL;
    GLComplete(callback, requestId, payload);
}

void _GameLoversGamePlatformAuthenticate(int requestId, GLGamePlatformCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        GKLocalPlayer *player = [GKLocalPlayer localPlayer];
        if (player.isAuthenticated)
        {
            GLComplete(callback, requestId, GLPlayerPayload(player));
            return;
        }

        // A second request while the first is still open fails fast instead of replacing its callback.
        if (gAuthCallback != NULL)
        {
            GLComplete(callback, requestId, nil);
            return;
        }

        gAuthRequestId = requestId;
        gAuthCallback = callback;

        // Game Center calls the handler again after the sign-in sheet closes and whenever the account changes,
        // so the pending request is completed once and later calls only refresh state.
        player.authenticateHandler = ^(UIViewController * _Nullable viewController, NSError * _Nullable error) {
            if (viewController != nil)
            {
                [UnityGetGLViewController() presentViewController:viewController animated:YES completion:nil];
                return;
            }

            GKLocalPlayer *current = [GKLocalPlayer localPlayer];
            if (error != nil) NSLog(@"[GameLovers.MobileServices] Game Center sign-in failed: %@", error);
            GLFinishAuthentication(current.isAuthenticated ? GLPlayerPayload(current) : nil);
        };
    });
}

int _GameLoversGamePlatformIsAuthenticated(void)
{
    return [GKLocalPlayer localPlayer].isAuthenticated ? 1 : 0;
}

#pragma mark - Leaderboards and achievements

void _GameLoversGamePlatformReportScore(const char *leaderboardId, long long score, int requestId, GLGamePlatformCallback callback)
{
    NSString *identifier = [NSString stringWithUTF8String:leaderboardId];
    [GKLeaderboard submitScore:(NSInteger)score
                       context:0
                        player:[GKLocalPlayer localPlayer]
                leaderboardIDs:@[identifier]
             completionHandler:^(NSError * _Nullable error) {
        if (error != nil) NSLog(@"[GameLovers.MobileServices] Submitting the score failed: %@", error);
        GLComplete(callback, requestId, error == nil ? @"ok" : nil);
    }];
}

static void GLReportAchievement(NSString *identifier, double percent, int requestId, GLGamePlatformCallback callback)
{
    GKAchievement *achievement = [[GKAchievement alloc] initWithIdentifier:identifier];
    achievement.percentComplete = MIN(100.0, MAX(0.0, percent));
    achievement.showsCompletionBanner = YES;
    [GKAchievement reportAchievements:@[achievement] withCompletionHandler:^(NSError * _Nullable error) {
        if (error != nil) NSLog(@"[GameLovers.MobileServices] Reporting the achievement failed: %@", error);
        GLComplete(callback, requestId, error == nil ? @"ok" : nil);
    }];
}

void _GameLoversGamePlatformUnlockAchievement(const char *achievementId, int requestId, GLGamePlatformCallback callback)
{
    GLReportAchievement([NSString stringWithUTF8String:achievementId], 100.0, requestId, callback);
}

// Game Center tracks a percentage, so the step count is converted with the achievement's total steps.
void _GameLoversGamePlatformIncrementAchievement(const char *achievementId, int steps, int totalSteps, int requestId, GLGamePlatformCallback callback)
{
    NSString *identifier = [NSString stringWithUTF8String:achievementId];
    if (totalSteps <= 0)
    {
        NSLog(@"[GameLovers.MobileServices] IncrementAchievement needs the achievement's total steps on iOS.");
        GLComplete(callback, requestId, nil);
        return;
    }

    [GKAchievement loadAchievementsWithCompletionHandler:^(NSArray<GKAchievement *> * _Nullable achievements, NSError * _Nullable error) {
        if (error != nil)
        {
            NSLog(@"[GameLovers.MobileServices] Loading achievement progress failed: %@", error);
            GLComplete(callback, requestId, nil);
            return;
        }

        double current = 0;
        for (GKAchievement *achievement in achievements)
        {
            if ([achievement.identifier isEqualToString:identifier]) current = achievement.percentComplete;
        }

        GLReportAchievement(identifier, current + (100.0 * steps / totalSteps), requestId, callback);
    }];
}

#pragma mark - Game Center views

@interface GLGameCenterDelegate : NSObject <GKGameCenterControllerDelegate>
@end

static GLGameCenterDelegate *gGameCenterDelegate = nil;

@implementation GLGameCenterDelegate
- (void)gameCenterViewControllerDidFinish:(GKGameCenterViewController *)gameCenterViewController
{
    [gameCenterViewController dismissViewControllerAnimated:YES completion:nil];
}
@end

static void GLPresentGameCenter(GKGameCenterViewController *controller)
{
    if (gGameCenterDelegate == nil) gGameCenterDelegate = [[GLGameCenterDelegate alloc] init];
    controller.gameCenterDelegate = gGameCenterDelegate;
    [UnityGetGLViewController() presentViewController:controller animated:YES completion:nil];
}

void _GameLoversGamePlatformShowLeaderboards(const char *leaderboardId)
{
    NSString *identifier = leaderboardId != NULL ? [NSString stringWithUTF8String:leaderboardId] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        GKGameCenterViewController *controller = identifier.length > 0
            ? [[GKGameCenterViewController alloc] initWithLeaderboardID:identifier playerScope:GKLeaderboardPlayerScopeGlobal timeScope:GKLeaderboardTimeScopeAllTime]
            : [[GKGameCenterViewController alloc] initWithState:GKGameCenterViewControllerStateLeaderboards];
        GLPresentGameCenter(controller);
    });
}

void _GameLoversGamePlatformShowAchievements(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        GLPresentGameCenter([[GKGameCenterViewController alloc] initWithState:GKGameCenterViewControllerStateAchievements]);
    });
}

#else

// Game Center is opt-in: without the capability enabled in the Mobile Services Config the bridge links no GameKit symbols.
static void GLGameCenterDisabled(GLGamePlatformCallback callback, int requestId)
{
    NSLog(@"[GameLovers.MobileServices] Game Center is not enabled for this build. Turn on the Game Center capability in the Mobile Services Config.");
    GLComplete(callback, requestId, nil);
}

void _GameLoversGamePlatformAuthenticate(int requestId, GLGamePlatformCallback callback) { GLGameCenterDisabled(callback, requestId); }
int _GameLoversGamePlatformIsAuthenticated(void) { return 0; }
void _GameLoversGamePlatformReportScore(const char *leaderboardId, long long score, int requestId, GLGamePlatformCallback callback) { GLGameCenterDisabled(callback, requestId); }
void _GameLoversGamePlatformUnlockAchievement(const char *achievementId, int requestId, GLGamePlatformCallback callback) { GLGameCenterDisabled(callback, requestId); }
void _GameLoversGamePlatformIncrementAchievement(const char *achievementId, int steps, int totalSteps, int requestId, GLGamePlatformCallback callback) { GLGameCenterDisabled(callback, requestId); }
void _GameLoversGamePlatformShowLeaderboards(const char *leaderboardId) { NSLog(@"[GameLovers.MobileServices] Game Center is not enabled for this build."); }
void _GameLoversGamePlatformShowAchievements(void) { NSLog(@"[GameLovers.MobileServices] Game Center is not enabled for this build."); }

#endif
