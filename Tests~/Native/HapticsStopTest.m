#import <Foundation/Foundation.h>
#import <CoreHaptics/CoreHaptics.h>
#import <dispatch/dispatch.h>
#include HAPTICS_SOURCE

@implementation UISelectionFeedbackGenerator
- (void)prepare {}
- (void)selectionChanged {}
@end

@implementation UINotificationFeedbackGenerator
- (void)prepare {}
- (void)notificationOccurred:(UINotificationFeedbackType)type {}
@end

@implementation UIImpactFeedbackGenerator
- (instancetype)initWithStyle:(UIImpactFeedbackStyle)style { return [super init]; }
- (void)prepare {}
- (void)impactOccurred {}
- (void)impactOccurredWithIntensity:(CGFloat)intensity {}
@end

@interface TestHapticPlayer : NSObject <CHHapticPatternPlayer>
@property BOOL isMuted;
@property NSInteger stopCount;
@property BOOL engineStopped;
@property BOOL throwsException;
@end

@implementation TestHapticPlayer
- (BOOL)startAtTime:(NSTimeInterval)time error:(NSError **)error { return YES; }
- (BOOL)stopAtTime:(NSTimeInterval)time error:(NSError **)error
{
    self.stopCount++;
    if (self.throwsException || (self.engineStopped && error == NULL))
    {
        @throw [NSException exceptionWithName:CoreHapticsErrorDomain reason:@"self.running" userInfo:@{@"OSStatus": @(-4805)}];
    }
    if (self.engineStopped)
    {
        *error = [NSError errorWithDomain:CoreHapticsErrorDomain code:CHHapticErrorCodeEngineNotRunning userInfo:nil];
        return NO;
    }
    return YES;
}
- (BOOL)sendParameters:(NSArray *)parameters atTime:(NSTimeInterval)time error:(NSError **)error { return YES; }
- (BOOL)scheduleParameterCurve:(CHHapticParameterCurve *)curve atTime:(NSTimeInterval)time error:(NSError **)error { return YES; }
- (BOOL)cancelAndReturnError:(NSError **)error { return YES; }
@end

@interface TestHapticEngine : NSObject
@property TestHapticPlayer *player;
@end

@implementation TestHapticEngine
- (BOOL)startAndReturnError:(NSError **)error { return YES; }
- (id<CHHapticPatternPlayer>)createPlayerWithPattern:(CHHapticPattern *)pattern error:(NSError **)error
{
    self.player = [TestHapticPlayer new];
    return self.player;
}
@end

static int failures;
static int executed;

static void Check(BOOL condition, const char *name)
{
    executed++;
    printf("%s %s\n", condition ? "PASS" : "FAIL", name);
    if (!condition) failures++;
}

static void CheckStop(BOOL engineStopped, BOOL throwsException, const char *name)
{
    TestHapticPlayer *player = [TestHapticPlayer new];
    player.engineStopped = engineStopped;
    player.throwsException = throwsException;
    gEnvelopePlayer = player;
    BOOL survived = YES;
    @try { GLHapticsStopEnvelope(); }
    @catch (NSException *exception) { survived = NO; }
    BOOL discarded = survived && player.stopCount == 1 && gEnvelopePlayer == nil;
    if (!survived) gEnvelopePlayer = nil;
    GLHapticsStopEnvelope();
    Check(discarded && player.stopCount == 1, name);
}

int main(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        @autoreleasepool
        {
            // ADMIT: Haptics.m GLHapticsStopEnvelope crashes stopping a player after engine shutdown.
            // RCR: Haptics.m GLHapticsStopEnvelope — original stop → RED (stopped engine and native exception). 2026-11-07
            CheckStop(NO, NO, "Stop_ActiveEngine_StopsAndDiscardsPlayer");
            CheckStop(YES, NO, "Stop_StoppedEngine_ReturnsErrorAndDiscardsPlayer");
            CheckStop(YES, YES, "Stop_NativeException_DiscardsPlayerWithoutCrashing");
            TestHapticEngine *engine = [TestHapticEngine new];
            gHapticEngine = (CHHapticEngine *)engine;
            BOOL played = GLHapticsPlayExplosionEnvelope(1.0f);
            TestHapticPlayer *replacement = engine.player;
            BOOL retained = replacement != nil && gEnvelopePlayer == replacement;
            GLHapticsStopEnvelope();
            Check(played && retained && replacement.stopCount == 1 && gEnvelopePlayer == nil, "Play_AfterStopFailure_CreatesAndStopsFreshEnvelope");
            if (failures > 0)
            {
                printf("Native haptics: %d executed, %d passed, %d failed\n", executed, executed - failures, failures);
                exit(EXIT_FAILURE);
            }

            TestHapticPlayer *player = [TestHapticPlayer new];
            player.engineStopped = YES;
            gEnvelopePlayer = player;
            gSelectionGen = [UISelectionFeedbackGenerator new];
            gNotificationGen = [UINotificationFeedbackGenerator new];
            gImpactGen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
            gLoopPresetId = GLHapticPresetIdExplosion;
            gLoopTimer = [NSTimer timerWithTimeInterval:1 repeats:YES block:^(NSTimer *timer) {}];
            NSTimer *timer = gLoopTimer;
            _GameLoversHapticsStop();
            dispatch_async(dispatch_get_main_queue(), ^{
                BOOL stopped = player.stopCount == 1 && gEnvelopePlayer == nil && gLoopTimer == nil && !timer.valid;
                BOOL cleared = gLoopPresetId == GLHapticPresetIdNone && gSelectionGen == nil && gNotificationGen == nil && gImpactGen == nil;
                Check(stopped && cleared, "ExportedStop_StoppedEngine_CancelsLoopAndClearsGenerators");
                printf("Native haptics: %d executed, %d passed, %d failed\n", executed, executed - failures, failures);
                exit(failures == 0 ? EXIT_SUCCESS : EXIT_FAILURE);
            });
        }
    });
    dispatch_main();
}
