#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

typedef void (*GLBackgroundTaskExpiredCallback)(int taskId);

// Our task id -> UIBackgroundTaskIdentifier. A task leaves the map when it ends; its expired flag stays until the
// managed handle is disposed, so IsExpired can be polled from any thread after the system ended the task.
static NSMutableDictionary<NSNumber *, NSNumber *> *gTasks = nil;
static NSMutableSet<NSNumber *> *gExpired = nil;
static int gNextTaskId = 1;

static NSObject *GLTaskLock(void)
{
    static NSObject *lock = nil;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        lock = [[NSObject alloc] init];
        gTasks = [NSMutableDictionary dictionary];
        gExpired = [NSMutableSet set];
    });
    return lock;
}

static void GLRunOnMainSync(void (^block)(void))
{
    if ([NSThread isMainThread])
    {
        block();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), block);
    }
}

static void GLEndNativeTask(int taskId)
{
    NSNumber *native = nil;
    @synchronized (GLTaskLock())
    {
        native = gTasks[@(taskId)];
        [gTasks removeObjectForKey:@(taskId)];
    }

    if (native != nil)
    {
        UIBackgroundTaskIdentifier identifier = (UIBackgroundTaskIdentifier)[native unsignedIntegerValue];
        // The system's expiration handler runs on the main thread and must end the task before it returns, so a
        // main-thread caller ends it immediately instead of queueing it.
        if ([NSThread isMainThread])
        {
            [[UIApplication sharedApplication] endBackgroundTask:identifier];
        }
        else
        {
            dispatch_async(dispatch_get_main_queue(), ^{
                [[UIApplication sharedApplication] endBackgroundTask:identifier];
            });
        }
    }
}

int _GameLoversBackgroundTaskBegin(const char *name, GLBackgroundTaskExpiredCallback callback)
{
    NSString *taskName = name != NULL ? [NSString stringWithUTF8String:name] : @"GameLovers.BackgroundTask";
    __block int taskId = 0;

    GLRunOnMainSync(^{
        int candidate;
        @synchronized (GLTaskLock())
        {
            candidate = gNextTaskId++;
        }

        UIBackgroundTaskIdentifier identifier = [[UIApplication sharedApplication] beginBackgroundTaskWithName:taskName expirationHandler:^{
            @synchronized (GLTaskLock())
            {
                [gExpired addObject:@(candidate)];
            }
            if (callback != NULL) callback(candidate);
            GLEndNativeTask(candidate);
        }];

        if (identifier == UIBackgroundTaskInvalid) return;

        @synchronized (GLTaskLock())
        {
            gTasks[@(candidate)] = @((NSUInteger)identifier);
        }
        taskId = candidate;
    });

    return taskId;
}

void _GameLoversBackgroundTaskEnd(int taskId)
{
    GLEndNativeTask(taskId);
    @synchronized (GLTaskLock())
    {
        [gExpired removeObject:@(taskId)];
    }
}

// Seconds left before suspension, or -1 while the application is in the foreground (no limit).
double _GameLoversBackgroundTaskRemaining(void)
{
    __block double remaining = -1;
    GLRunOnMainSync(^{
        NSTimeInterval value = [UIApplication sharedApplication].backgroundTimeRemaining;
        remaining = value > 1e9 ? -1 : value;
    });
    return remaining;
}

int _GameLoversBackgroundTaskIsExpired(int taskId)
{
    @synchronized (GLTaskLock())
    {
        return [gExpired containsObject:@(taskId)] ? 1 : 0;
    }
}
