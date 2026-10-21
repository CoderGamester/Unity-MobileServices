#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>

#import "GLPermissionGroups.h"

#if GL_PERMISSION_CAMERA || GL_PERMISSION_MICROPHONE
#import <AVFoundation/AVFoundation.h>
#endif
#if GL_PERMISSION_LOCATION
#import <CoreLocation/CoreLocation.h>
#endif
#if GL_PERMISSION_PHOTOS || GL_PERMISSION_PHOTOS_ADD
#import <Photos/Photos.h>
#endif

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
    GLPermissionStatusRestricted    = 3,
    GLPermissionStatusLimited       = 4
};

static void GLSendResult(int requestId, NSString *callbackGameObject, NSString *callbackMethod, GLPermissionStatus status)
{
    NSString *payload = [NSString stringWithFormat:@"%d:%ld", requestId, (long)status];
    UnitySendMessage([callbackGameObject UTF8String], [callbackMethod UTF8String], [payload UTF8String]);
}

__attribute__((unused)) static void GLSendUndeclared(int requestId, NSString *callbackGameObject, NSString *callbackMethod, const char *group)
{
    NSLog(@"[GameLovers.MobileServices] The %s permission group is not compiled into this build. Add its usage description to the Mobile Services Config.", group);
    GLSendResult(requestId, callbackGameObject, callbackMethod, GLPermissionStatusNotDetermined);
}

#pragma mark - Notifications (always compiled: no Info.plist purpose string exists for them)

static GLPermissionStatus MapUNAuthorizationStatus(UNAuthorizationStatus s)
{
    switch (s)
    {
        case UNAuthorizationStatusAuthorized:    return GLPermissionStatusGranted;
        case UNAuthorizationStatusProvisional:   return GLPermissionStatusLimited;
        case UNAuthorizationStatusDenied:        return GLPermissionStatusDenied;
        case UNAuthorizationStatusEphemeral:     return GLPermissionStatusGranted;
        case UNAuthorizationStatusNotDetermined:
        default:                                 return GLPermissionStatusNotDetermined;
    }
}

// The notification state is only readable asynchronously, so Check serves a cache that is refreshed at
// startup, whenever the app becomes active (the player may have changed it in Settings) and after each request.
static GLPermissionStatus gNotificationStatus = GLPermissionStatusNotDetermined;
static BOOL gNotificationStatusValid = NO;
static dispatch_semaphore_t gNotificationReady = nil;

static void GLRefreshNotificationStatus(void (^completion)(GLPermissionStatus status))
{
    [[UNUserNotificationCenter currentNotificationCenter] getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings * _Nonnull settings) {
        GLPermissionStatus status = MapUNAuthorizationStatus(settings.authorizationStatus);
        BOOL firstValue = !gNotificationStatusValid;
        gNotificationStatus = status;
        gNotificationStatusValid = YES;
        if (firstValue) dispatch_semaphore_signal(gNotificationReady);
        if (completion != nil) completion(status);
    }];
}

static void GLEnsureNotificationTracking(void)
{
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        gNotificationReady = dispatch_semaphore_create(0);
        [[NSNotificationCenter defaultCenter] addObserverForName:UIApplicationDidBecomeActiveNotification
                                                          object:nil
                                                           queue:nil
                                                      usingBlock:^(NSNotification * _Nonnull note) {
            GLRefreshNotificationStatus(nil);
        }];
        GLRefreshNotificationStatus(nil);
    });
}

static GLPermissionStatus GLCheckNotifications(void)
{
    GLEnsureNotificationTracking();
    if (!gNotificationStatusValid)
    {
        // Cold start only: wait briefly for the first read; every later call is served from the cache.
        dispatch_semaphore_wait(gNotificationReady, dispatch_time(DISPATCH_TIME_NOW, 250 * NSEC_PER_MSEC));
    }
    return gNotificationStatusValid ? gNotificationStatus : GLPermissionStatusNotDetermined;
}

static void GLRequestNotifications(int requestId, NSString *goName, NSString *methodName)
{
    GLEnsureNotificationTracking();
    UNAuthorizationOptions options = UNAuthorizationOptionAlert | UNAuthorizationOptionBadge | UNAuthorizationOptionSound;
    [[UNUserNotificationCenter currentNotificationCenter]
        requestAuthorizationWithOptions:options
                      completionHandler:^(BOOL granted, NSError * _Nullable error) {
        GLRefreshNotificationStatus(^(GLPermissionStatus status) {
            GLSendResult(requestId, goName, methodName, status);
        });
    }];
}

#pragma mark - Camera and microphone

#if GL_PERMISSION_CAMERA || GL_PERMISSION_MICROPHONE
static GLPermissionStatus MapAVAuthorizationStatus(AVAuthorizationStatus s)
{
    switch (s)
    {
        case AVAuthorizationStatusAuthorized:    return GLPermissionStatusGranted;
        case AVAuthorizationStatusDenied:        return GLPermissionStatusDenied;
        case AVAuthorizationStatusRestricted:    return GLPermissionStatusRestricted;
        case AVAuthorizationStatusNotDetermined:
        default:                                 return GLPermissionStatusNotDetermined;
    }
}

static GLPermissionStatus GLCheckAV(AVMediaType mediaType)
{
    return MapAVAuthorizationStatus([AVCaptureDevice authorizationStatusForMediaType:mediaType]);
}

static void GLRequestAV(AVMediaType mediaType, int requestId, NSString *goName, NSString *methodName)
{
    [AVCaptureDevice requestAccessForMediaType:mediaType completionHandler:^(BOOL granted) {
        // The post-request status keeps Restricted distinct from a user denial.
        GLSendResult(requestId, goName, methodName, GLCheckAV(mediaType));
    }];
}
#endif

#pragma mark - Location

#if GL_PERMISSION_LOCATION
static GLPermissionStatus GLLocationStatus(CLLocationManager *manager, BOOL requestedAlways)
{
    BOOL reducedAccuracy = manager.accuracyAuthorization == CLAccuracyAuthorizationReducedAccuracy;
    switch (manager.authorizationStatus)
    {
        case kCLAuthorizationStatusAuthorizedAlways:
            return reducedAccuracy ? GLPermissionStatusLimited : GLPermissionStatusGranted;
        case kCLAuthorizationStatusAuthorizedWhenInUse:
            return (requestedAlways || reducedAccuracy) ? GLPermissionStatusLimited : GLPermissionStatusGranted;
        case kCLAuthorizationStatusDenied:
            return GLPermissionStatusDenied;
        case kCLAuthorizationStatusRestricted:
            return GLPermissionStatusRestricted;
        case kCLAuthorizationStatusNotDetermined:
        default:
            return GLPermissionStatusNotDetermined;
    }
}

// Held to keep CLLocationManager alive long enough for the delegate callback.
@interface GLLocationDelegate : NSObject <CLLocationManagerDelegate>
@property (nonatomic, assign) int requestId;
@property (nonatomic, assign) BOOL requestedAlways;
@property (nonatomic, copy)   NSString *callbackGameObject;
@property (nonatomic, copy)   NSString *callbackMethod;
@property (nonatomic, strong) CLLocationManager *manager;
@end

static NSMutableArray<GLLocationDelegate *> *gLocationDelegates = nil;

@implementation GLLocationDelegate

- (void)locationManagerDidChangeAuthorization:(CLLocationManager *)manager
{
    GLPermissionStatus status = GLLocationStatus(manager, _requestedAlways);
    if (status == GLPermissionStatusNotDetermined)
    {
        return; // Wait for the user to actually decide before responding.
    }

    GLSendResult(_requestId, _callbackGameObject, _callbackMethod, status);

    _manager.delegate = nil;
    _manager = nil;
    [gLocationDelegates removeObject:self];
}

@end

static GLPermissionStatus GLCheckLocation(BOOL requestedAlways)
{
    return GLLocationStatus([[CLLocationManager alloc] init], requestedAlways);
}

static void GLRequestLocation(BOOL always, int requestId, NSString *goName, NSString *methodName)
{
    if (gLocationDelegates == nil)
    {
        gLocationDelegates = [NSMutableArray array];
    }

    GLLocationDelegate *delegate = [[GLLocationDelegate alloc] init];
    delegate.requestId          = requestId;
    delegate.requestedAlways    = always;
    delegate.callbackGameObject = goName;
    delegate.callbackMethod     = methodName;
    delegate.manager            = [[CLLocationManager alloc] init];
    delegate.manager.delegate   = delegate;
    [gLocationDelegates addObject:delegate];

    if (always)
    {
        [delegate.manager requestAlwaysAuthorization];
    }
    else
    {
        [delegate.manager requestWhenInUseAuthorization];
    }
}
#endif

#pragma mark - Photos

#if GL_PERMISSION_PHOTOS || GL_PERMISSION_PHOTOS_ADD
static GLPermissionStatus MapPHAuthorizationStatus(PHAuthorizationStatus s)
{
    switch (s)
    {
        case PHAuthorizationStatusAuthorized: return GLPermissionStatusGranted;
        case PHAuthorizationStatusLimited:    return GLPermissionStatusLimited;
        case PHAuthorizationStatusDenied:     return GLPermissionStatusDenied;
        case PHAuthorizationStatusRestricted: return GLPermissionStatusRestricted;
        case PHAuthorizationStatusNotDetermined:
        default:                              return GLPermissionStatusNotDetermined;
    }
}

static GLPermissionStatus GLCheckPhotos(PHAccessLevel level)
{
    return MapPHAuthorizationStatus([PHPhotoLibrary authorizationStatusForAccessLevel:level]);
}

static void GLRequestPhotos(PHAccessLevel level, int requestId, NSString *goName, NSString *methodName)
{
    [PHPhotoLibrary requestAuthorizationForAccessLevel:level handler:^(PHAuthorizationStatus status) {
        GLSendResult(requestId, goName, methodName, MapPHAuthorizationStatus(status));
    }];
}
#endif

#pragma mark - Exports

int _GameLoversPermissionsCheck(int permissionId)
{
    switch ((GLAppPermission)permissionId)
    {
#if GL_PERMISSION_CAMERA
        case GLAppPermissionCamera:
            return GLCheckAV(AVMediaTypeVideo);
#endif
#if GL_PERMISSION_MICROPHONE
        case GLAppPermissionMicrophone:
            return GLCheckAV(AVMediaTypeAudio);
#endif
#if GL_PERMISSION_LOCATION
        case GLAppPermissionLocationWhenInUse:
            return GLCheckLocation(NO);
        case GLAppPermissionLocationAlways:
            return GLCheckLocation(YES);
#endif
#if GL_PERMISSION_PHOTOS
        case GLAppPermissionPhotoLibrary:
            return GLCheckPhotos(PHAccessLevelReadWrite);
#endif
#if GL_PERMISSION_PHOTOS_ADD
        case GLAppPermissionPhotoLibraryAddOnly:
            return GLCheckPhotos(PHAccessLevelAddOnly);
#endif
        case GLAppPermissionNotifications:
            return GLCheckNotifications();
        default:
            return GLPermissionStatusNotDetermined;
    }
}

void _GameLoversPermissionsRequest(int permissionId, int requestId, const char *callbackGameObject, const char *callbackMethod)
{
    NSString *goName     = [NSString stringWithUTF8String:callbackGameObject];
    NSString *methodName = [NSString stringWithUTF8String:callbackMethod];

    switch ((GLAppPermission)permissionId)
    {
        case GLAppPermissionCamera:
#if GL_PERMISSION_CAMERA
            GLRequestAV(AVMediaTypeVideo, requestId, goName, methodName);
#else
            GLSendUndeclared(requestId, goName, methodName, "camera");
#endif
            return;
        case GLAppPermissionMicrophone:
#if GL_PERMISSION_MICROPHONE
            GLRequestAV(AVMediaTypeAudio, requestId, goName, methodName);
#else
            GLSendUndeclared(requestId, goName, methodName, "microphone");
#endif
            return;
        case GLAppPermissionLocationWhenInUse:
        case GLAppPermissionLocationAlways:
#if GL_PERMISSION_LOCATION
            GLRequestLocation(permissionId == GLAppPermissionLocationAlways, requestId, goName, methodName);
#else
            GLSendUndeclared(requestId, goName, methodName, "location");
#endif
            return;
        case GLAppPermissionPhotoLibrary:
#if GL_PERMISSION_PHOTOS
            GLRequestPhotos(PHAccessLevelReadWrite, requestId, goName, methodName);
#else
            GLSendUndeclared(requestId, goName, methodName, "photos");
#endif
            return;
        case GLAppPermissionPhotoLibraryAddOnly:
#if GL_PERMISSION_PHOTOS_ADD
            GLRequestPhotos(PHAccessLevelAddOnly, requestId, goName, methodName);
#else
            GLSendUndeclared(requestId, goName, methodName, "photos add-only");
#endif
            return;
        case GLAppPermissionNotifications:
            GLRequestNotifications(requestId, goName, methodName);
            return;
    }

    GLSendResult(requestId, goName, methodName, GLPermissionStatusNotDetermined);
}
