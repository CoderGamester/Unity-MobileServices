#import <Foundation/Foundation.h>

extern void UnitySendMessage(const char *gameObject, const char *method, const char *message);

static id gLowPowerObserver = nil;

bool _GameLoversBatteryIsLowPowerModeEnabled(void)
{
    return [[NSProcessInfo processInfo] isLowPowerModeEnabled];
}

void _GameLoversBatteryStartObservingLowPowerMode(void)
{
    if (gLowPowerObserver != nil)
    {
        return;
    }

    gLowPowerObserver = [[NSNotificationCenter defaultCenter]
        addObserverForName:NSProcessInfoPowerStateDidChangeNotification
                    object:nil
                     queue:[NSOperationQueue mainQueue]
                usingBlock:^(NSNotification * _Nonnull note) {
            // The shared DeviceServicesHost MonoBehaviour is named "DeviceServicesHost" and
            // exposes OnIosLowPowerModeChanged(string) as a public method invokable by SendMessage.
            UnitySendMessage("DeviceServicesHost", "OnIosLowPowerModeChanged", "");
        }];
}

void _GameLoversBatteryStopObservingLowPowerMode(void)
{
    if (gLowPowerObserver == nil)
    {
        return;
    }

    [[NSNotificationCenter defaultCenter] removeObserver:gLowPowerObserver];
    gLowPowerObserver = nil;
}
