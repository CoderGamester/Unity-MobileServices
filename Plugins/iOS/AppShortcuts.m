#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <objc/runtime.h>

// A tapped shortcut waits here until managed code collects it: on a cold start UIKit delivers it before Unity's C# runs.
static NSString *gPendingShortcutUri = nil;
static IMP gOriginalPerformAction = NULL;

typedef void (^GLShortcutCompletion)(BOOL);

static void GLPerformActionForShortcut(id self, SEL _cmd, UIApplication *application, UIApplicationShortcutItem *item, GLShortcutCompletion completion)
{
    id uri = item.userInfo[@"uri"];
    if ([uri isKindOfClass:[NSString class]])
    {
        @synchronized ([NSNull null])
        {
            gPendingShortcutUri = uri;
        }
    }

    // An existing implementation (Firebase, another SDK, the game) keeps running and owns the completion call.
    if (gOriginalPerformAction != NULL)
    {
        ((void (*)(id, SEL, UIApplication *, UIApplicationShortcutItem *, GLShortcutCompletion))gOriginalPerformAction)(self, _cmd, application, item, completion);
        return;
    }

    completion([uri isKindOfClass:[NSString class]]);
}

@interface GLShortcutInstaller : NSObject
@end

@implementation GLShortcutInstaller

// Installed at load time because UIKit asks the delegate about a cold-start shortcut right after launch.
+ (void)load
{
    Class delegateClass = NSClassFromString(@"UnityAppController");
    if (delegateClass == Nil)
    {
        NSLog(@"[GameLovers.MobileServices] UnityAppController was not found; tapped shortcuts will not reach the game.");
        return;
    }

    SEL selector = @selector(application:performActionForShortcutItem:completionHandler:);
    Method existing = class_getInstanceMethod(delegateClass, selector);
    if (existing != NULL)
    {
        gOriginalPerformAction = method_setImplementation(existing, (IMP)GLPerformActionForShortcut);
        return;
    }

    class_addMethod(delegateClass, selector, (IMP)GLPerformActionForShortcut, "v@:@@@?");
}

@end

void _GameLoversShortcutsSet(const char *json)
{
    NSData *data = [[NSString stringWithUTF8String:json] dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *root = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    NSMutableArray<UIApplicationShortcutItem *> *items = [NSMutableArray array];
    for (NSDictionary *entry in root[@"items"])
    {
        NSString *subtitle = entry[@"subtitle"];
        [items addObject:[[UIApplicationShortcutItem alloc] initWithType:entry[@"id"]
                                                          localizedTitle:entry[@"title"]
                                                       localizedSubtitle:subtitle.length > 0 ? subtitle : nil
                                                                    icon:nil
                                                                userInfo:@{ @"uri": entry[@"uri"] }]];
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        [UIApplication sharedApplication].shortcutItems = items;
    });
}

void _GameLoversShortcutsClear(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [UIApplication sharedApplication].shortcutItems = @[];
    });
}

// Returns the URI of a shortcut tapped since the last call as a copy the caller reads, or NULL.
const char *_GameLoversShortcutTakePending(void)
{
    NSString *uri = nil;
    @synchronized ([NSNull null])
    {
        uri = gPendingShortcutUri;
        gPendingShortcutUri = nil;
    }
    return uri == nil ? NULL : strdup([uri UTF8String]);
}
