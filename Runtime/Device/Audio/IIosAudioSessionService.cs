// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Configures the iOS audio session so app audio is not muted by the device's silent (mute) switch.
	/// On Android / Editor / unsupported platforms every method is a safe no-op.
	/// </summary>
	/// <remarks>
	/// Also exposed via <see cref="IDeviceService.AudioSession"/> for one-stop discovery.
	/// </remarks>
	public interface IIosAudioSessionService
	{
		/// <summary>
		/// Sets the iOS <c>AVAudioSession</c> category to <c>AVAudioSessionCategoryPlayback</c> so audio
		/// keeps playing even when the device's ringer/silent switch is on.
		///
		/// Idempotent and safe to call from any state. Call once at app startup, before any audio plays.
		/// </summary>
		void ConfigureForPlayback();
	}
}
