#import <Foundation/Foundation.h>

typedef NS_ENUM(NSInteger, UIImpactFeedbackStyle)
{
    UIImpactFeedbackStyleLight,
    UIImpactFeedbackStyleMedium,
    UIImpactFeedbackStyleHeavy,
    UIImpactFeedbackStyleSoft,
    UIImpactFeedbackStyleRigid
};

typedef NS_ENUM(NSInteger, UINotificationFeedbackType)
{
    UINotificationFeedbackTypeSuccess,
    UINotificationFeedbackTypeWarning,
    UINotificationFeedbackTypeError
};

@interface UISelectionFeedbackGenerator : NSObject
- (void)prepare;
- (void)selectionChanged;
@end

@interface UINotificationFeedbackGenerator : NSObject
- (void)prepare;
- (void)notificationOccurred:(UINotificationFeedbackType)type;
@end

@interface UIImpactFeedbackGenerator : NSObject
- (instancetype)initWithStyle:(UIImpactFeedbackStyle)style;
- (void)prepare;
- (void)impactOccurred;
- (void)impactOccurredWithIntensity:(CGFloat)intensity;
@end
