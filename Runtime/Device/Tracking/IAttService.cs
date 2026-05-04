using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>App Tracking Transparency authorization status (mirrors iOS <c>ATTrackingManagerAuthorizationStatus</c>).</summary>
	public enum AttStatus
	{
		NotDetermined = 0,
		Restricted    = 1,
		Denied        = 2,
		Authorized    = 3,
	}

	/// <summary>
	/// iOS 14.5+ App Tracking Transparency. Android / Editor / unsupported platforms always return
	/// <see cref="AttStatus.Authorized"/> (no equivalent restriction).
	/// </summary>
	/// <remarks>
	/// Built directly on <c>ATTrackingManager</c> with no dependency on the deprecation-bound
	/// <c>com.unity.ads.ios-support</c> package.
	/// </remarks>
	public interface IAttService
	{
		/// <summary>Current authorization status without prompting.</summary>
		AttStatus CurrentStatus { get; }

		/// <summary>
		/// Requests tracking authorization. Idempotent: if the user has already responded (granted,
		/// denied, or restricted) the OS returns the previous decision without showing the prompt again.
		/// </summary>
		Task<AttStatus> RequestAuthorizationAsync();
	}
}
