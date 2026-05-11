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

	/// <summary>Result of a permission check / request.</summary>
	public enum PermissionStatus
	{
		NotDetermined,
		Denied,
		Granted,
		Restricted,
	}

	/// <summary>
	/// Unified iOS+Android runtime-permissions service.
	/// </summary>
	/// <remarks>
	/// Uses <see cref="Task{TResult}"/> rather than <c>UniTask</c> to avoid pulling in a new package dependency.
	/// </remarks>
	public interface IPermissionsService
	{
		/// <summary>Returns the current status without prompting the user. Synchronous.</summary>
		PermissionStatus Check(AppPermission permission);

		/// <summary>Requests the permission, prompting the user if not yet determined. Idempotent if already granted/denied.</summary>
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
