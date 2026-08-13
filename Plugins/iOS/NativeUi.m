#import <UIKit/UIKit.h>
#import <StoreKit/StoreKit.h>

extern UIViewController *UnityGetGLViewController();

NSString *ToNSString(char* string) {
    return [NSString stringWithUTF8String:string];
}

typedef void (*AlertButtonCallback)(const char * str);

static UIAlertController *GameLoversCurrentAlert;

void _GameLoversAlertMessage (bool isSheet, char* title, char* message, char* buttonsText[], int buttonsStyle[], int buttonsLength, AlertButtonCallback buttonCallback)
{
    UIAlertControllerStyle style = isSheet ? UIAlertControllerStyleActionSheet : UIAlertControllerStyleAlert;
    UIAlertController *alert = [UIAlertController alertControllerWithTitle:ToNSString(title) message:ToNSString(message) preferredStyle:style];

    for (int i = 0; i < buttonsLength; i++)
    {
        NSString *buttonText = ToNSString(buttonsText[i]);
        UIAlertAction * button = [UIAlertAction actionWithTitle:buttonText style:(UIAlertActionStyle)buttonsStyle[i] handler:^(UIAlertAction * action)
        {
            GameLoversCurrentAlert = nil;
            buttonCallback((char*)[buttonText UTF8String]);
        }];
        [alert addAction:button];
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        void (^presentAlert)(void) = ^{
            GameLoversCurrentAlert = alert;
            [UnityGetGLViewController() presentViewController:alert animated:YES completion:nil];
        };
        if (GameLoversCurrentAlert != nil)
        {
            [GameLoversCurrentAlert dismissViewControllerAnimated:NO completion:presentAlert];
        }
        else
        {
            presentAlert();
        }
    });
}

void _GameLoversDismissAlert(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [GameLoversCurrentAlert dismissViewControllerAnimated:YES completion:nil];
        GameLoversCurrentAlert = nil;
    });
}

void _GameLoversToastMessage (char* message, BOOL isLongDuration)
{
    float duration = isLongDuration ? 3.5 : 2;
    UIAlertController *alert = [UIAlertController alertControllerWithTitle:nil message:ToNSString(message) preferredStyle:UIAlertControllerStyleAlert];

    [UnityGetGLViewController() presentViewController:alert animated:YES completion:nil];

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, duration * NSEC_PER_SEC), dispatch_get_main_queue(), ^{
        [alert dismissViewControllerAnimated:YES completion:nil];
    });
}

void _GameLoversRequestReview(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 14.0, *))
        {
            UIViewController *vc = UnityGetGLViewController();
            UIWindowScene *scene = (UIWindowScene *)vc.view.window.windowScene;
            if (scene != nil)
            {
                [SKStoreReviewController requestReviewInScene:scene];
                return;
            }
        }
        // iOS 10.3 - 13.x fallback (and the unlikely case where windowScene is nil on iOS 14+).
        if ([SKStoreReviewController respondsToSelector:@selector(requestReview)])
        {
            [SKStoreReviewController requestReview];
        }
    });
}

void _GameLoversShare(const char *text, const char *url, const char *imagePath)
{
    NSString *nsText      = (text != NULL && *text != 0)           ? [NSString stringWithUTF8String:text]      : nil;
    NSString *nsUrl       = (url != NULL && *url != 0)             ? [NSString stringWithUTF8String:url]       : nil;
    NSString *nsImagePath = (imagePath != NULL && *imagePath != 0) ? [NSString stringWithUTF8String:imagePath] : nil;

    dispatch_async(dispatch_get_main_queue(), ^{
        NSMutableArray *items = [NSMutableArray array];

        if (nsText != nil)
        {
            [items addObject:nsText];
        }

        if (nsUrl != nil)
        {
            NSURL *parsedUrl = [NSURL URLWithString:nsUrl];
            if (parsedUrl != nil)
            {
                [items addObject:parsedUrl];
            }
            else
            {
                [items addObject:nsUrl];
            }
        }

        if (nsImagePath != nil)
        {
            UIImage *image = [UIImage imageWithContentsOfFile:nsImagePath];
            if (image != nil)
            {
                [items addObject:image];
            }
            else
            {
                NSLog(@"[GameLovers.MobileServices] Share: failed to load image at path %@", nsImagePath);
            }
        }

        if ([items count] == 0)
        {
            NSLog(@"[GameLovers.MobileServices] Share called with no content; skipping.");
            return;
        }

        UIActivityViewController *activityVc = [[UIActivityViewController alloc] initWithActivityItems:items applicationActivities:nil];
        UIViewController *root = UnityGetGLViewController();

        // iPad popover anchor: centre of the root view, zero-sized rect (avoids assertion).
        if (activityVc.popoverPresentationController != nil)
        {
            activityVc.popoverPresentationController.sourceView = root.view;
            activityVc.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds),
                                                                              CGRectGetMidY(root.view.bounds),
                                                                              0,
                                                                              0);
            activityVc.popoverPresentationController.permittedArrowDirections = 0;
        }

        [root presentViewController:activityVc animated:YES completion:nil];
    });
}
