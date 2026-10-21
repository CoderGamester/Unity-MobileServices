#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <PhotosUI/PhotosUI.h>
#import "GLPermissionGroups.h"

#if GL_PERMISSION_PHOTOS_ADD
#import <Photos/Photos.h>
#endif

extern UIViewController *UnityGetGLViewController(void);

// Completion callback into managed code. `result` is NULL on failure or cancellation. Always invoked on the main queue.
typedef void (*GLMediaCallback)(int requestId, const char *result);

static void GLCompleteMedia(GLMediaCallback callback, int requestId, NSString *result)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (callback != NULL) callback(requestId, result != nil ? [result UTF8String] : NULL);
    });
}

#pragma mark - Save to gallery

// Saving needs add-only photo authorization, so it only exists when NSPhotoLibraryAddUsageDescription is declared.
void _GameLoversMediaSave(const char *path, int isVideo, int requestId, GLMediaCallback callback)
{
#if GL_PERMISSION_PHOTOS_ADD
    NSURL *fileUrl = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
    [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
        if (isVideo != 0)
        {
            [PHAssetChangeRequest creationRequestForAssetFromVideoAtFileURL:fileUrl];
        }
        else
        {
            [PHAssetChangeRequest creationRequestForAssetFromImageAtFileURL:fileUrl];
        }
    } completionHandler:^(BOOL success, NSError * _Nullable error) {
        if (!success) NSLog(@"[GameLovers.MobileServices] SaveToGallery failed: %@", error);
        GLCompleteMedia(callback, requestId, success ? @"ok" : nil);
    }];
#else
    NSLog(@"[GameLovers.MobileServices] SaveToGallery needs a PhotoLibraryAddOnly usage description (NSPhotoLibraryAddUsageDescription); the save code is not compiled into this build.");
    GLCompleteMedia(callback, requestId, nil);
#endif
}

#pragma mark - Pick image

@interface GLImagePickerDelegate : NSObject <PHPickerViewControllerDelegate>
@property (nonatomic, assign) int requestId;
@property (nonatomic, assign) GLMediaCallback callback;
@end

static GLImagePickerDelegate *gActivePicker = nil;

@implementation GLImagePickerDelegate

- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results API_AVAILABLE(ios(14))
{
    [picker dismissViewControllerAnimated:YES completion:nil];

    int requestId = _requestId;
    GLMediaCallback callback = _callback;
    gActivePicker = nil;

    NSItemProvider *provider = results.firstObject.itemProvider;
    if (provider == nil || ![provider hasItemConformingToTypeIdentifier:@"public.image"])
    {
        GLCompleteMedia(callback, requestId, nil);
        return;
    }

    // The provider's file is deleted when the handler returns, so it is copied to a path the game owns first.
    [provider loadFileRepresentationForTypeIdentifier:@"public.image" completionHandler:^(NSURL * _Nullable url, NSError * _Nullable error) {
        if (url == nil)
        {
            NSLog(@"[GameLovers.MobileServices] PickImage failed to load the selection: %@", error);
            GLCompleteMedia(callback, requestId, nil);
            return;
        }

        NSString *extension = url.pathExtension.length > 0 ? url.pathExtension : @"img";
        NSString *name = [NSString stringWithFormat:@"gamelovers-picked-%@.%@", [[NSUUID UUID] UUIDString], extension];
        NSURL *destination = [NSURL fileURLWithPath:[NSTemporaryDirectory() stringByAppendingPathComponent:name]];
        NSError *copyError = nil;
        if (![[NSFileManager defaultManager] copyItemAtURL:url toURL:destination error:&copyError])
        {
            NSLog(@"[GameLovers.MobileServices] PickImage failed to copy the selection: %@", copyError);
            GLCompleteMedia(callback, requestId, nil);
            return;
        }

        GLCompleteMedia(callback, requestId, destination.path);
    }];
}

@end

// The system picker runs out of process and needs neither a permission nor a usage description.
void _GameLoversMediaPickImage(int requestId, GLMediaCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (gActivePicker != nil)
        {
            GLCompleteMedia(callback, requestId, nil);
            return;
        }

        PHPickerConfiguration *configuration = [[PHPickerConfiguration alloc] init];
        configuration.selectionLimit = 1;
        configuration.filter = [PHPickerFilter imagesFilter];

        GLImagePickerDelegate *delegate = [[GLImagePickerDelegate alloc] init];
        delegate.requestId = requestId;
        delegate.callback = callback;
        gActivePicker = delegate;

        PHPickerViewController *picker = [[PHPickerViewController alloc] initWithConfiguration:configuration];
        picker.delegate = delegate;
        [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
    });
}
