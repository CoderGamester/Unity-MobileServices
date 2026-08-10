#import <UIKit/UIKit.h>

// Preset ids must mirror GameLovers.MobileServices.Haptics.HapticPreset enum.
typedef NS_ENUM(NSInteger, GLHapticPresetId)
{
    GLHapticPresetIdNone         = 0,
    GLHapticPresetIdSelection    = 1,
    GLHapticPresetIdSuccess      = 2,
    GLHapticPresetIdWarning      = 3,
    GLHapticPresetIdError        = 4,
    GLHapticPresetIdImpactLight  = 5,
    GLHapticPresetIdImpactMedium = 6,
    GLHapticPresetIdImpactHeavy  = 7,
    GLHapticPresetIdImpactRigid  = 8,
    GLHapticPresetIdImpactSoft   = 9
};

// Reusable generators. Lazily created on first use; reset to nil on Stop so the next call
// gets a freshly-prepared generator (Apple docs: prepare/play within ~100ms for best feel).
static UISelectionFeedbackGenerator    *gSelectionGen    = nil;
static UINotificationFeedbackGenerator *gNotificationGen = nil;
static UIImpactFeedbackGenerator       *gImpactGen       = nil;
static NSTimer                         *gLoopTimer       = nil;
static GLHapticPresetId                 gLoopPresetId    = GLHapticPresetIdNone;

static void GLHapticsPlaySelection(void)
{
    if (gSelectionGen == nil)
    {
        gSelectionGen = [[UISelectionFeedbackGenerator alloc] init];
    }
    [gSelectionGen prepare];
    [gSelectionGen selectionChanged];
}

static void GLHapticsPlayNotification(UINotificationFeedbackType type)
{
    if (gNotificationGen == nil)
    {
        gNotificationGen = [[UINotificationFeedbackGenerator alloc] init];
    }
    [gNotificationGen prepare];
    [gNotificationGen notificationOccurred:type];
}

static void GLHapticsPlayImpact(UIImpactFeedbackStyle style)
{
    // UIImpactFeedbackGenerator is bound to a single style at init time, so we re-create
    // when the requested style changes. Keeping it cached when reused (e.g. looping a single style).
    static UIImpactFeedbackStyle sLastStyle = (UIImpactFeedbackStyle)-1;
    if (gImpactGen == nil || sLastStyle != style)
    {
        gImpactGen  = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        sLastStyle  = style;
    }
    [gImpactGen prepare];
    [gImpactGen impactOccurred];
}

static void GLHapticsPlayPresetById(GLHapticPresetId presetId)
{
    switch (presetId)
    {
        case GLHapticPresetIdNone:
            return;
        case GLHapticPresetIdSelection:
            GLHapticsPlaySelection();
            return;
        case GLHapticPresetIdSuccess:
            GLHapticsPlayNotification(UINotificationFeedbackTypeSuccess);
            return;
        case GLHapticPresetIdWarning:
            GLHapticsPlayNotification(UINotificationFeedbackTypeWarning);
            return;
        case GLHapticPresetIdError:
            GLHapticsPlayNotification(UINotificationFeedbackTypeError);
            return;
        case GLHapticPresetIdImpactLight:
            GLHapticsPlayImpact(UIImpactFeedbackStyleLight);
            return;
        case GLHapticPresetIdImpactMedium:
            GLHapticsPlayImpact(UIImpactFeedbackStyleMedium);
            return;
        case GLHapticPresetIdImpactHeavy:
            GLHapticsPlayImpact(UIImpactFeedbackStyleHeavy);
            return;
        case GLHapticPresetIdImpactRigid:
            if (@available(iOS 13.0, *))
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleRigid);
            }
            else
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleHeavy);
            }
            return;
        case GLHapticPresetIdImpactSoft:
            if (@available(iOS 13.0, *))
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleSoft);
            }
            else
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleLight);
            }
            return;
    }
}

static void GLHapticsCancelLoopTimer(void)
{
    if (gLoopTimer != nil)
    {
        [gLoopTimer invalidate];
        gLoopTimer = nil;
    }
    gLoopPresetId = GLHapticPresetIdNone;
}

static void GLHapticsLoopTick(NSTimer *timer)
{
    (void)timer;
    GLHapticsPlayPresetById(gLoopPresetId);
}

void _GameLoversHapticsPreset(int presetId)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        GLHapticsPlayPresetById((GLHapticPresetId)presetId);
    });
}

void _GameLoversHapticsLoopStart(int presetId)
{
    // Loop interval is intentionally short (~120ms) so the device feels continuously vibrating;
    // each tick re-fires the system feedback generator which emits a sub-100ms haptic.
    dispatch_async(dispatch_get_main_queue(), ^{
        GLHapticsCancelLoopTimer();
        gLoopPresetId = (GLHapticPresetId)presetId;
        GLHapticsPlayPresetById(gLoopPresetId);
        gLoopTimer = [NSTimer scheduledTimerWithTimeInterval:0.12
                                                     repeats:YES
                                                       block:^(NSTimer *t) { GLHapticsLoopTick(t); }];
    });
}

void _GameLoversHapticsCustom(float intensity, float durationMs)
{
    // UIKit feedback generators don't expose intensity directly. We approximate by mapping the
    // [0,1] intensity to one of three impact styles, fire it, and (if duration > ~150ms) loop a
    // similar pattern until the C# auto-stop coroutine fires _GameLoversHapticsStop().
    dispatch_async(dispatch_get_main_queue(), ^{
        UIImpactFeedbackStyle style;
        if (intensity < 0.34f)
        {
            style = UIImpactFeedbackStyleLight;
        }
        else if (intensity < 0.67f)
        {
            style = UIImpactFeedbackStyleMedium;
        }
        else
        {
            style = UIImpactFeedbackStyleHeavy;
        }
        GLHapticsPlayImpact(style);

        // Custom haptics with finite duration > ~150ms loop the chosen style on a timer until Stop.
        if (durationMs > 150.0f)
        {
            GLHapticsCancelLoopTimer();
            // Re-use the loop timer infrastructure: pretend it's the matching impact preset.
            switch (style)
            {
                case UIImpactFeedbackStyleLight:  gLoopPresetId = GLHapticPresetIdImpactLight;  break;
                case UIImpactFeedbackStyleMedium: gLoopPresetId = GLHapticPresetIdImpactMedium; break;
                case UIImpactFeedbackStyleHeavy:  gLoopPresetId = GLHapticPresetIdImpactHeavy;  break;
                default:                          gLoopPresetId = GLHapticPresetIdImpactMedium; break;
            }
            gLoopTimer = [NSTimer scheduledTimerWithTimeInterval:0.12
                                                         repeats:YES
                                                           block:^(NSTimer *t) { GLHapticsLoopTick(t); }];
        }
    });
}

void _GameLoversHapticsStop(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        GLHapticsCancelLoopTimer();
        gSelectionGen    = nil;
        gNotificationGen = nil;
        gImpactGen       = nil;
    });
}
