#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>

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
    GLHapticPresetIdImpactSoft   = 9,
    GLHapticPresetIdQuickImpactHeavy = 10,
    GLHapticPresetIdExplosion    = 11
};

// Reusable generators. Lazily created on first use; reset to nil on Stop so the next call
// gets a freshly-prepared generator (Apple docs: prepare/play within ~100ms for best feel).
static UISelectionFeedbackGenerator    *gSelectionGen    = nil;
static UINotificationFeedbackGenerator *gNotificationGen = nil;
static UIImpactFeedbackGenerator       *gImpactGen       = nil;
static NSTimer                         *gLoopTimer       = nil;
static GLHapticPresetId                 gLoopPresetId    = GLHapticPresetIdNone;

// Core Haptics state for envelope presets. The engine lives for the process; only the player is replaced per play.
static CHHapticEngine                  *gHapticEngine API_AVAILABLE(ios(13.0)) = nil;
static id<CHHapticPatternPlayer>        gEnvelopePlayer API_AVAILABLE(ios(13.0)) = nil;
static BOOL                             gHapticEngineUnavailable = NO;

// Explosion intensity steps in seconds and [0, 1]; must mirror HapticEnvelopes.cs so iOS and Android share one curve.
static const float GLExplosionStepSeconds = 0.04f;
static const float GLExplosionSteps[]     = { 1.000f, 0.698f, 0.462f, 0.294f, 0.194f, 0.160f };
static const int   GLExplosionStepCount   = sizeof(GLExplosionSteps) / sizeof(GLExplosionSteps[0]);

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

static void GLHapticsPlayImpact(UIImpactFeedbackStyle style, float intensity)
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
    if (@available(iOS 13.0, *))
    {
        [gImpactGen impactOccurredWithIntensity:intensity];
    }
    else
    {
        [gImpactGen impactOccurred];
    }
}

static CHHapticEngine *GLHapticsEngine(void) API_AVAILABLE(ios(13.0))
{
    if (gHapticEngineUnavailable)
    {
        return nil;
    }

    if (gHapticEngine == nil)
    {
        if (!CHHapticEngine.capabilitiesForHardware.supportsHaptics)
        {
            gHapticEngineUnavailable = YES;
            return nil;
        }

        NSError *error = nil;
        gHapticEngine = [[CHHapticEngine alloc] initAndReturnError:&error];
        if (gHapticEngine == nil)
        {
            gHapticEngineUnavailable = YES;
            return nil;
        }

        gHapticEngine.playsHapticsOnly = YES;
        gHapticEngine.autoShutdownEnabled = YES;
        gHapticEngine.resetHandler = ^{
            gEnvelopePlayer = nil;
        };
    }

    NSError *startError = nil;
    return [gHapticEngine startAndReturnError:&startError] ? gHapticEngine : nil;
}

static void GLHapticsStopEnvelope(void)
{
    if (@available(iOS 13.0, *))
    {
        if (gEnvelopePlayer != nil)
        {
            [gEnvelopePlayer stopAtTime:CHHapticTimeImmediate error:nil];
            gEnvelopePlayer = nil;
        }
    }
}

static BOOL GLHapticsPlayExplosionEnvelope(float intensity) API_AVAILABLE(ios(13.0))
{
    CHHapticEngine *engine = GLHapticsEngine();
    if (engine == nil)
    {
        return NO;
    }

    NSTimeInterval duration = GLExplosionStepSeconds * GLExplosionStepCount;
    NSArray<CHHapticEventParameter *> *parameters = @[
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity],
        [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness value:0.35f],
    ];
    CHHapticEvent *event = [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous
                                                         parameters:parameters
                                                       relativeTime:0
                                                           duration:duration];

    NSMutableArray<CHHapticParameterCurveControlPoint *> *points = [NSMutableArray arrayWithCapacity:GLExplosionStepCount + 1];
    for (int i = 0; i < GLExplosionStepCount; i++)
    {
        [points addObject:[[CHHapticParameterCurveControlPoint alloc] initWithRelativeTime:GLExplosionStepSeconds * i value:GLExplosionSteps[i]]];
    }
    [points addObject:[[CHHapticParameterCurveControlPoint alloc] initWithRelativeTime:duration value:GLExplosionSteps[GLExplosionStepCount - 1]]];
    CHHapticParameterCurve *curve = [[CHHapticParameterCurve alloc] initWithParameterID:CHHapticDynamicParameterIDHapticIntensityControl
                                                                          controlPoints:points
                                                                           relativeTime:0];

    NSError *error = nil;
    CHHapticPattern *pattern = [[CHHapticPattern alloc] initWithEvents:@[event] parameterCurves:@[curve] error:&error];
    id<CHHapticPatternPlayer> player = pattern != nil ? [engine createPlayerWithPattern:pattern error:&error] : nil;
    if (player == nil || ![player startAtTime:CHHapticTimeImmediate error:&error])
    {
        return NO;
    }

    gEnvelopePlayer = player;
    return YES;
}

static void GLHapticsPlayExplosion(float intensity)
{
    if (@available(iOS 13.0, *))
    {
        if (GLHapticsPlayExplosionEnvelope(intensity))
        {
            return;
        }
    }

    GLHapticsPlayImpact(UIImpactFeedbackStyleHeavy, intensity);
}

static void GLHapticsPlayPresetById(GLHapticPresetId presetId, float intensity)
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
            GLHapticsPlayImpact(UIImpactFeedbackStyleLight, intensity);
            return;
        case GLHapticPresetIdImpactMedium:
            GLHapticsPlayImpact(UIImpactFeedbackStyleMedium, intensity);
            return;
        case GLHapticPresetIdImpactHeavy:
        case GLHapticPresetIdQuickImpactHeavy:
            GLHapticsPlayImpact(UIImpactFeedbackStyleHeavy, intensity);
            return;
        case GLHapticPresetIdImpactRigid:
            if (@available(iOS 13.0, *))
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleRigid, intensity);
            }
            else
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleHeavy, intensity);
            }
            return;
        case GLHapticPresetIdImpactSoft:
            if (@available(iOS 13.0, *))
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleSoft, intensity);
            }
            else
            {
                GLHapticsPlayImpact(UIImpactFeedbackStyleLight, intensity);
            }
            return;
        case GLHapticPresetIdExplosion:
            GLHapticsPlayExplosion(intensity);
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
    GLHapticsPlayPresetById(gLoopPresetId, 1.0f);
}

void _GameLoversHapticsPreset(int presetId, float intensityScale)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        GLHapticsPlayPresetById((GLHapticPresetId)presetId, intensityScale);
    });
}

void _GameLoversHapticsLoopStart(int presetId)
{
    // Loop interval is intentionally short (~120ms) so the device feels continuously vibrating;
    // each tick re-fires the system feedback generator which emits a sub-100ms haptic.
    dispatch_async(dispatch_get_main_queue(), ^{
        GLHapticsCancelLoopTimer();
        gLoopPresetId = (GLHapticPresetId)presetId;
        GLHapticsPlayPresetById(gLoopPresetId, 1.0f);
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
        GLHapticsPlayImpact(style, 1.0f);

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
        GLHapticsStopEnvelope();
        gSelectionGen    = nil;
        gNotificationGen = nil;
        gImpactGen       = nil;
    });
}
