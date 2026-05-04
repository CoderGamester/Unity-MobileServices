#import <AVFoundation/AVFoundation.h>

void _SetAudioSessionPlayback(void)
{
    NSError *err = nil;
    AVAudioSession *session = [AVAudioSession sharedInstance];

    BOOL ok = [session setCategory:AVAudioSessionCategoryPlayback error:&err];
    if (!ok || err != nil)
    {
        NSLog(@"[GameLovers.MobileServices] setCategory:AVAudioSessionCategoryPlayback failed: %@", err);
        return;
    }

    err = nil;
    ok = [session setActive:YES error:&err];
    if (!ok || err != nil)
    {
        NSLog(@"[GameLovers.MobileServices] AVAudioSession setActive:YES failed: %@", err);
    }
}
