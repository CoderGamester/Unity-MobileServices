// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Controls whether the device screen should stay awake (override the OS sleep timeout).
	/// </summary>
	public interface IScreenWakeService
	{
		/// <summary>
		/// When <c>true</c>, sets <c>Screen.sleepTimeout</c> to <c>SleepTimeout.NeverSleep</c>;
		/// when <c>false</c>, restores <c>SleepTimeout.SystemSetting</c>. Idempotent.
		/// </summary>
		bool KeepAwake { get; set; }
	}
}
