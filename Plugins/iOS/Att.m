#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

extern void UnitySendMessage(const char *gameObject, const char *method, const char *message);

// Mirrors GameLovers.MobileServices.Device.AttStatus.
typedef NS_ENUM(NSInteger, GLAttStatus)
{
    GLAttStatusNotDetermined = 0,
    GLAttStatusRestricted    = 1,
    GLAttStatusDenied        = 2,
    GLAttStatusAuthorized    = 3
};

static GLAttStatus GLMapAttStatus(ATTrackingManagerAuthorizationStatus s)
{
    switch (s)
    {
        case ATTrackingManagerAuthorizationStatusNotDetermined: return GLAttStatusNotDetermined;
        case ATTrackingManagerAuthorizationStatusRestricted:    return GLAttStatusRestricted;
        case ATTrackingManagerAuthorizationStatusDenied:        return GLAttStatusDenied;
        case ATTrackingManagerAuthorizationStatusAuthorized:    return GLAttStatusAuthorized;
        default:                                                return GLAttStatusNotDetermined;
    }
}

int _GameLoversAttCurrentStatus(void)
{
    if (@available(iOS 14, *))
    {
        return GLMapAttStatus([ATTrackingManager trackingAuthorizationStatus]);
    }
    return GLAttStatusAuthorized;
}

void _GameLoversAttRequestAuthorization(int requestId, const char *callbackGameObject, const char *callbackMethod)
{
    NSString *goName     = [NSString stringWithUTF8String:callbackGameObject];
    NSString *methodName = [NSString stringWithUTF8String:callbackMethod];

    if (@available(iOS 14, *))
    {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            NSString *payload = [NSString stringWithFormat:@"%d:%ld", requestId, (long)GLMapAttStatus(status)];
            UnitySendMessage([goName UTF8String], [methodName UTF8String], [payload UTF8String]);
        }];
    }
    else
    {
        NSString *payload = [NSString stringWithFormat:@"%d:%ld", requestId, (long)GLAttStatusAuthorized];
        UnitySendMessage([goName UTF8String], [methodName UTF8String], [payload UTF8String]);
    }
}
