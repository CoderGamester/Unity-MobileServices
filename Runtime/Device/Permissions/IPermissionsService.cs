using System.Collections.Generic;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Cross-platform permissions catalog.
	/// </summary>
	public enum AppPermission
	{
		Camera,
		Microphone,
		LocationWhenInUse,
		LocationAlways,
		PhotoLibrary,
		PhotoLibraryAddOnly,
		Notifications,
	}

	/// <summary>
	/// Result of a permission check / request.
	/// </summary>
	public enum PermissionStatus
	{
		NotDetermined, // 0
		Denied, // 1
		Granted, // 2
		Restricted, // 3
		Limited, // 4: partial access (selected photos, provisional notifications, approximate or when-in-use-only location)
	}

	/// <summary>
	/// Unified iOS+Android runtime-permissions service.
	/// </summary>
	/// <remarks>
	/// Treat <see cref="PermissionStatus.Granted"/> and <see cref="PermissionStatus.Limited"/> as usable access.
	/// A <see cref="PermissionStatus.Denied"/> permission usually cannot be requested again: explain why it is
	/// needed and send the player to <see cref="DeviceService.OpenAppSettings"/>.
	/// </remarks>
	/// <remarks>
	/// Uses <see cref="Task{TResult}"/> rather than <c>UniTask</c> to avoid pulling in a new package dependency.
	/// </remarks>
	public interface IPermissionsService
	{
		/// <summary>
		/// Returns the current status without prompting the user. Synchronous.
		/// </summary>
		PermissionStatus Check(AppPermission permission);

		/// <summary>
		/// Requests the permission, prompting the user if not yet determined. Idempotent if the
		/// permission has already been granted or denied.
		/// </summary>
		Task<PermissionStatus> RequestAsync(AppPermission permission);

		/// <summary>
		/// Convenience for the common multi-permission flows (e.g. camera+mic for video chat). Awaits
		/// each <see cref="RequestAsync(AppPermission)"/> sequentially — iOS prompts cannot stack — and
		/// returns a dictionary keyed by permission.
		/// </summary>
		/// <remarks>
		/// Default interface method — implementations may override, but the default behaviour is
		/// expected to be sufficient for the vast majority of consumers.
		/// </remarks>
		async Task<IReadOnlyDictionary<AppPermission, PermissionStatus>> RequestAsync(params AppPermission[] permissions)
		{
			var result = new Dictionary<AppPermission, PermissionStatus>();
			if (permissions == null) return result;
			foreach (var p in permissions)
			{
				result[p] = await RequestAsync(p);
			}
			return result;
		}
	}
}
