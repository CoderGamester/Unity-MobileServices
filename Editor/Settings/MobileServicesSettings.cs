using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Settings
{
	/// <summary>
	/// Per-locale text for one configured usage description (e.g. <c>NSCameraUsageDescription</c>).
	/// </summary>
	[Serializable]
	public sealed class LocaleEntry
	{
		[SerializeField] public string LocaleCode = "en";
		[TextArea(2, 4)]
		[SerializeField] public string UsageDescription;
	}

	[Serializable]
	public sealed class PermissionUsageRow
	{
		[SerializeField] public AppPermission Permission;
		[SerializeField] public List<LocaleEntry> Entries = new List<LocaleEntry> { new LocaleEntry() };
	}

	[Serializable]
	public sealed class AttUsageRow
	{
		[SerializeField] public List<LocaleEntry> Entries = new List<LocaleEntry> { new LocaleEntry() };
	}

	[Serializable]
	public sealed class CapabilityToggles
	{
		[SerializeField] public bool PushNotifications;
		[SerializeField] public bool BackgroundAudio;
		[SerializeField] public bool AppTracking;
		[SerializeField] public bool AssociatedDomains;
		[SerializeField] public List<string> AssociatedDomainList = new List<string>();
	}

	[Serializable]
	public sealed class AndroidManifestToggles
	{
		[SerializeField] public bool ReadMediaImages;
		[SerializeField] public bool PostNotifications;
		[SerializeField] public bool RecordAudio;
		[SerializeField] public bool Camera;
		[SerializeField] public bool AccessFineLocation;
		[SerializeField] public bool IncludeShareQueriesBlock;
	}

	/// <summary>
	/// Editor-only project settings for the Mobile Services build pipeline. Persisted to
	/// <c>ProjectSettings/MobileServicesSettings.asset</c> (project-shared — commit to VCS).
	/// See <c>docs/build-pipeline.md</c> for the full schema and behaviour.
	/// </summary>
	[FilePath("ProjectSettings/MobileServicesSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	public sealed class MobileServicesSettings : ScriptableSingleton<MobileServicesSettings>
	{
		[SerializeField] private List<PermissionUsageRow> _permissionDescriptions = new List<PermissionUsageRow>();
		[SerializeField] private AttUsageRow _attUsageDescription = new AttUsageRow();
		[SerializeField] private CapabilityToggles _capabilities = new CapabilityToggles();
		[SerializeField] private AndroidManifestToggles _androidManifest = new AndroidManifestToggles();
		[SerializeField] private bool _allowPlaceholderUsageDescriptions;
		[SerializeField] private bool _scanPopulatedCapabilities;
		[SerializeField] private bool _enableRuntimeSimulatorOverlay;

		/// <summary>Per-permission usage description rows. Reads as read-only; mutate via the explicit helpers.</summary>
		public IReadOnlyList<PermissionUsageRow> PermissionDescriptions => _permissionDescriptions;

		/// <summary>ATT (`NSUserTrackingUsageDescription`) per-locale row.</summary>
		public AttUsageRow AttUsageDescription => _attUsageDescription;

		public CapabilityToggles Capabilities => _capabilities;
		public AndroidManifestToggles AndroidManifest => _androidManifest;

		/// <summary>
		/// CI / preview-build soft mode. When <c>true</c>, the iOS postprocessor injects a
		/// <c>[GameLovers placeholder]</c> string for any missing usage description instead of failing
		/// the build. Apple WILL reject a submission that ships these placeholders — by design.
		/// </summary>
		public bool AllowPlaceholderUsageDescriptions
		{
			get => _allowPlaceholderUsageDescriptions;
			set
			{
				_allowPlaceholderUsageDescriptions = value;
				Save(true);
			}
		}

		/// <summary>True once the user has hit "Scan project for used services" at least once.</summary>
		public bool ScanPopulatedCapabilities
		{
			get => _scanPopulatedCapabilities;
			set
			{
				_scanPopulatedCapabilities = value;
				Save(true);
			}
		}

		/// <summary>
		/// Opt-in: spawn the editor-only runtime simulator overlay (UIDocument inside the Game /
		/// Simulator view) on its own whenever the user enters play mode, even when the Device
		/// Simulator panel is not open. The overlay paints the truth-mirror mocks pixel-aligned with
		/// the simulated device's <c>Screen.*</c> values so what designers see matches what Apple's
		/// reviewer would see. Default OFF — this standalone play-mode spawn is opt-in to avoid a
		/// <c>DontDestroyOnLoad</c> GameObject in projects that have no use for it. (When the Device
		/// Simulator panel is open the overlay is kept alive regardless of this setting.)
		/// </summary>
		public bool EnableRuntimeSimulatorOverlay
		{
			get => _enableRuntimeSimulatorOverlay;
			set
			{
				_enableRuntimeSimulatorOverlay = value;
				Save(true);
			}
		}

		/// <summary>
		/// Returns the row for <paramref name="permission"/>, creating a fresh one with a default
		/// <c>en</c> locale entry if none exists yet.
		/// </summary>
		public PermissionUsageRow GetOrAddRow(AppPermission permission)
		{
			foreach (var row in _permissionDescriptions)
			{
				if (row.Permission == permission)
				{
					return row;
				}
			}
			var newRow = new PermissionUsageRow { Permission = permission };
			_permissionDescriptions.Add(newRow);
			Save(true);
			return newRow;
		}

		/// <summary>Sets the English usage description for the given permission. Convenience wrapper.</summary>
		public void SetUsageDescriptionEn(AppPermission permission, string text)
		{
			var row = GetOrAddRow(permission);
			SetLocaleEntry(row.Entries, "en", text);
			Save(true);
		}

		public void SetAttUsageDescriptionEn(string text)
		{
			SetLocaleEntry(_attUsageDescription.Entries, "en", text);
			Save(true);
		}

		public string GetUsageDescriptionEn(AppPermission permission)
		{
			foreach (var row in _permissionDescriptions)
			{
				if (row.Permission != permission) continue;
				foreach (var entry in row.Entries)
				{
					if (entry.LocaleCode == "en") return entry.UsageDescription;
				}
			}
			return null;
		}

		public string GetAttUsageDescriptionEn()
		{
			foreach (var entry in _attUsageDescription.Entries)
			{
				if (entry.LocaleCode == "en") return entry.UsageDescription;
			}
			return null;
		}

		public void Persist() => Save(true);

		private static void SetLocaleEntry(List<LocaleEntry> entries, string locale, string text)
		{
			foreach (var entry in entries)
			{
				if (entry.LocaleCode == locale)
				{
					entry.UsageDescription = text;
					return;
				}
			}
			entries.Add(new LocaleEntry { LocaleCode = locale, UsageDescription = text });
		}

		/// <summary>
		/// Returns a per-permission "Suggested copy" usage description. These follow Apple's review
		/// guidelines (concrete, user-visible benefit) so the team isn't left to write the wording
		/// from scratch.
		/// </summary>
		public static string GetSuggestedCopy(AppPermission permission)
		{
			switch (permission)
			{
				case AppPermission.Camera:
					return "Allows you to take photos and videos to share inside the app.";
				case AppPermission.Microphone:
					return "Allows you to record audio for voice chat and clip sharing.";
				case AppPermission.LocationWhenInUse:
					return "Lets us show nearby content while you have the app open.";
				case AppPermission.LocationAlways:
					return "Lets us notify you about nearby events even when the app is in the background.";
				case AppPermission.PhotoLibrary:
					return "Allows you to attach photos from your library to your in-app content.";
				case AppPermission.PhotoLibraryAddOnly:
					return "Lets us save the screenshots and recordings you make in the app to your library.";
				case AppPermission.Notifications:
					return "Allows us to send you reward reminders and important game updates.";
				default:
					return string.Empty;
			}
		}

		public static string GetSuggestedAttCopy() =>
			"Your data will be used to provide a better personalised experience and to support our developers.";

		/// <summary>
		/// Maps <see cref="AppPermission"/> to the iOS Info.plist key the postprocessor must inject.
		/// </summary>
		public static string GetIosUsageKey(AppPermission permission)
		{
			switch (permission)
			{
				case AppPermission.Camera:               return "NSCameraUsageDescription";
				case AppPermission.Microphone:           return "NSMicrophoneUsageDescription";
				case AppPermission.LocationWhenInUse:    return "NSLocationWhenInUseUsageDescription";
				case AppPermission.LocationAlways:       return "NSLocationAlwaysAndWhenInUseUsageDescription";
				case AppPermission.PhotoLibrary:         return "NSPhotoLibraryUsageDescription";
				case AppPermission.PhotoLibraryAddOnly:  return "NSPhotoLibraryAddUsageDescription";
				case AppPermission.Notifications:        return null; // No Info.plist key on iOS.
				default:                                 return null;
			}
		}

		/// <summary>
		/// Returns the set of permissions that have empty English usage descriptions but ARE referenced
		/// by the project (per <paramref name="referenced"/>). The build postprocessor consults this
		/// to decide whether to fail or soft-warn.
		/// </summary>
		public IReadOnlyList<AppPermission> GetMissingUsageDescriptions(IEnumerable<AppPermission> referenced)
		{
			var missing = new List<AppPermission>();
			foreach (var p in referenced)
			{
				if (GetIosUsageKey(p) == null)
				{
					continue;
				}
				var text = GetUsageDescriptionEn(p);
				if (string.IsNullOrWhiteSpace(text))
				{
					missing.Add(p);
				}
			}
			return missing;
		}

		/// <summary>True when ATT capability is enabled but no English usage description is configured.</summary>
		public bool IsAttUsageDescriptionMissing()
		{
			if (!_capabilities.AppTracking)
			{
				return false;
			}
			return string.IsNullOrWhiteSpace(GetAttUsageDescriptionEn());
		}
	}
}
