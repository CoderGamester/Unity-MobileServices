#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <CoreLocation/CoreLocation.h>
#import <Photos/Photos.h>
#import <UserNotifications/UserNotifications.h>

extern void UnitySendMessage(const char *gameObject, const char *method, const char *message);

// Mirrors GameLovers.MobileServices.Device.AppPermission enum.
typedef NS_ENUM(NSInteger, GLAppPermission)
{
    GLAppPermissionCamera              = 0,
    GLAppPermissionMicrophone          = 1,
    GLAppPermissionLocationWhenInUse   = 2,
    GLAppPermissionLocationAlways      = 3,
    GLAppPermissionPhotoLibrary        = 4,
    GLAppPermissionPhotoLibraryAddOnly = 5,
    GLAppPermissionNotifications       = 6
};

// Mirrors GameLovers.MobileServices.Device.PermissionStatus enum.
typedef NS_ENUM(NSInteger, GLPermissionStatus)
{
    GLPermissionStatusNotDetermined = 0,
    GLPermissionStatusDenied        = 1,
    GLPermissionStatusGranted       = 2,
    GLPermissionStatusRestricted    = 3
};

// Held to keep CLLocationManager alive long enough for the delegate callback.
@interface GLLocationDelegate : NSObject <CLLocationManagerDelegate>
@property (nonatomic, assign) int requestId;
@property (nonatomic, copy)   NSString *callbackGameObject;
@property (nonatomic, copy)   NSString *callbackMethod;
@property (nonatomic, strong) CLLocationManager *manager;
@end

@implementation GLLocationDelegate

- (void)locationManagerDidChangeAuthorization:(CLLocationManager *)manager
{
    GLPermissionStatus status = GLPermissionStatusNotDetermined;
    switch (manager.authorizationStatus)
    {
        case kCLAuthorizationStatusAuthorizedAlways:
        case kCLAuthorizationStatusAuthorizedWhenInUse:
            status = GLPermissionStatusGranted;
            break;
        case kCLAuthorizationStatusDenied:
            status = GLPermissionStatusDenied;
            break;
        case kCLAuthorizationStatusRestricted:
            status = GLPermissionStatusRestricted;
            break;
        case kCLAuthorizationStatusNotDetermined:
        default:
            return; // Wait for the user to actually decide before responding.
    }

    NSString *payload = [NSString stringWithFormat:@"%d:%ld", _requestId, (long)status];
    UnitySendMessage([_callbackGameObject UTF8String], [_callbackMethod UTF8String], [payload UTF8String]);

    _manager.delegate = nil;
    _manager = nil;
}

@end

static NSMutableArray<GLLocationDelegate *> *gLocationDelegates = nil;

static GLPermissionStatus MapAVAuthorizationStatus(AVAuthorizationStatus s)
{
    switch (s)
    {
        case AVAuthorizationStatusAuthorized:    return GLPermissionStatusGranted;
        case AVAuthorizationStatusDenied:        return GLPermissionStatusDenied;
        case AVAuthorizationStatusRestricted:    return GLPermissionStatusRestricted;
        case AVAuthorizationStatusNotDetermined: return GLPermissionStatusNotDetermined;
        default:                                 return GLPermissionStatusNotDetermined;
    }
}

static GLPermissionStatus MapPHAuthorizationStatus(PHAuthorizationStatus s)
{
    switch (s)
    {
        case PHAuthorizationStatusAuthorized: return GLPermissionStatusGranted;
        case PHAuthorizationStatusLimited:    return GLPermissionStatusGranted;
        case PHAuthorizationStatusDenied:     return GLPermissionStatusDenied;
        case PHAuthorizationStatusRestricted: return GLPermissionStatusRestricted;
        case PHAuthorizationStatusNotDetermined:
        default:                              return GLPermissionStatusNotDetermined;
    }
}

static GLPermissionStatus MapUNAuthorizationStatus(UNAuthorizationStatus s)
{
    switch (s)
    {
        case UNAuthorizationStatusAuthorized:    return GLPermissionStatusGranted;
        case UNAuthorizationStatusProvisional:   return GLPermissionStatusGranted;
        case UNAuthorizationStatusDenied:        return GLPermissionStatusDenied;
        case UNAuthorizationStatusEphemeral:     return GLPermissionStatusGranted;
        case UNAuthorizationStatusNotDetermined:
        default:                                 return GLPermissionStatusNotDetermined;
    }
}

static GLPermissionStatus MapCLAuthorizationStatusValue(CLAuthorizationStatus s)
{
    switch (s)
    {
        case kCLAuthorizationStatusAuthorizedAlways:
        case kCLAuthorizationStatusAuthorizedWhenInUse:
            return GLPermissionStatusGranted;
        case kCLAuthorizationStatusDenied:
            return GLPermissionStatusDenied;
        case kCLAuthorizationStatusRestricted:
            return GLPermissionStatusRestricted;
        case kCLAuthorizationStatusNotDetermined:
        default:
            return GLPermissionStatusNotDetermined;
    }
}

int _GameLoversPermissionsCheck(int permissionId)
{
    GLAppPermission p = (GLAppPermission)permissionId;
    switch (p)
    {
        case GLAppPermissionCamera:
            return MapAVAuthorizationStatus([AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo]);
        case GLAppPermissionMicrophone:
            return MapAVAuthorizationStatus([AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeAudio]);
        case GLAppPermissionLocationWhenInUse:
        case GLAppPermissionLocationAlways:
            return MapCLAuthorizationStatusValue([CLLocationManager authorizationStatus]);
        case GLAppPermissionPhotoLibrary:
            if (@available(iOS 14.0, *))
            {
                return MapPHAuthorizationStatus([PHPhotoLibrary authorizationStatusForAccessLevel:PHAccessLevelReadWrite]);
            }
            return MapPHAuthorizationStatus([PHPhotoLibrary authorizationStatus]);
        case GLAppPermissionPhotoLibraryAddOnly:
            if (@available(iOS 14.0, *))
            {
                return MapPHAuthorizationStatus([PHPhotoLibrary authorizationStatusForAccessLevel:PHAccessLevelAddOnly]);
            }
            return MapPHAuthorizationStatus([PHPhotoLibrary authorizationStatus]);
        case GLAppPermissionNotifications:
        {
            __block GLPermissionStatus result = GLPermissionStatusNotDetermined;
            dispatch_semaphore_t sem = dispatch_semaphore_create(0);
            [[UNUserNotificationCenter currentNotificationCenter] getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings * _Nonnull settings) {
                result = MapUNAuthorizationStatus(settings.authorizationStatus);
                dispatch_semaphore_signal(sem);
            }];
            dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 1 * NSEC_PER_SEC));
            return result;
        }
    }
    return GLPermissionStatusNotDetermined;
}

static void GLSendResult(int requestId, const char *callbackGameObject, const char *callbackMethod, GLPermissionStatus status)
{
    NSString *payload = [NSString stringWithFormat:@"%d:%ld", requestId, (long)status];
    UnitySendMessage(callbackGameObject, callbackMethod, [payload UTF8String]);
}

void _GameLoversPermissionsRequest(int permissionId, int requestId, const char *callbackGameObject, const char *callbackMethod)
{
    NSString *goName     = [NSString stringWithUTF8String:callbackGameObject];
    NSString *methodName = [NSString stringWithUTF8String:callbackMethod];
    GLAppPermission p    = (GLAppPermission)permissionId;

    switch (p)
    {
        case GLAppPermissionCamera:
        {
            [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted) {
                GLSendResult(requestId, [goName UTF8String], [methodName UTF8String],
                             granted ? GLPermissionStatusGranted : GLPermissionStatusDenied);
            }];
            return;
        }
        case GLAppPermissionMicrophone:
        {
            [AVCaptureDevice requestAccessForMediaType:AVMediaTypeAudio completionHandler:^(BOOL granted) {
                GLSendResult(requestId, [goName UTF8String], [methodName UTF8String],
                             granted ? GLPermissionStatusGranted : GLPermissionStatusDenied);
            }];
            return;
        }
        case GLAppPermissionLocationWhenInUse:
        case GLAppPermissionLocationAlways:
        {
            if (gLocationDelegates == nil)
            {
                gLocationDelegates = [NSMutableArray array];
            }

            GLLocationDelegate *delegate = [[GLLocationDelegate alloc] init];
            delegate.requestId          = requestId;
            delegate.callbackGameObject = goName;
            delegate.callbackMethod     = methodName;
            delegate.manager            = [[CLLocationManager alloc] init];
            delegate.manager.delegate   = delegate;
            [gLocationDelegates addObject:delegate];

            if (p == GLAppPermissionLocationAlways)
            {
                [delegate.manager requestAlwaysAuthorization];
            }
            else
            {
                [delegate.manager requestWhenInUseAuthorization];
            }
            return;
        }
        case GLAppPermissionPhotoLibrary:
        {
            if (@available(iOS 14.0, *))
            {
                [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelReadWrite handler:^(PHAuthorizationStatus status) {
                    GLSendResult(requestId, [goName UTF8String], [methodName UTF8String], MapPHAuthorizationStatus(status));
                }];
            }
            else
            {
                [PHPhotoLibrary requestAuthorization:^(PHAuthorizationStatus status) {
                    GLSendResult(requestId, [goName UTF8String], [methodName UTF8String], MapPHAuthorizationStatus(status));
                }];
            }
            return;
        }
        case GLAppPermissionPhotoLibraryAddOnly:
        {
            if (@available(iOS 14.0, *))
            {
                [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly handler:^(PHAuthorizationStatus status) {
                    GLSendResult(requestId, [goName UTF8String], [methodName UTF8String], MapPHAuthorizationStatus(status));
                }];
            }
            else
            {
                [PHPhotoLibrary requestAuthorization:^(PHAuthorizationStatus status) {
                    GLSendResult(requestId, [goName UTF8String], [methodName UTF8String], MapPHAuthorizationStatus(status));
                }];
            }
            return;
        }
        case GLAppPermissionNotifications:
        {
            UNAuthorizationOptions options = UNAuthorizationOptionAlert | UNAuthorizationOptionBadge | UNAuthorizationOptionSound;
            [[UNUserNotificationCenter currentNotificationCenter]
                requestAuthorizationWithOptions:options
                              completionHandler:^(BOOL granted, NSError * _Nullable error) {
                GLSendResult(requestId, [goName UTF8String], [methodName UTF8String],
                             granted ? GLPermissionStatusGranted : GLPermissionStatusDenied);
            }];
            return;
        }
    }

    GLSendResult(requestId, [goName UTF8String], [methodName UTF8String], GLPermissionStatusNotDetermined);
}
