using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Settings
{
	/// <summary>
	/// One localized value for a usage description (e.g. <c>NSCameraUsageDescription</c>). The
	/// <see cref="LocaleCode"/> is an ISO language code (<c>en</c>, <c>fr</c>, <c>pt-BR</c>, …) and maps
	/// to an iOS <c>&lt;locale&gt;.lproj/InfoPlist.strings</c> entry at build time so the OS shows the
	/// right text for the device language.
	/// </summary>
	[Serializable]
	public sealed class LocaleEntry
	{
		/// <summary>ISO language code for this value (<c>en</c>, <c>fr</c>, <c>pt-BR</c>, …).</summary>
		[SerializeField] public string LocaleCode = MobileServicesConfig.DefaultLocaleCode;

		/// <summary>The usage-description text shown to the user for <see cref="LocaleCode"/>.</summary>
		[TextArea(2, 4)]
		[SerializeField] public string UsageDescription;
	}

	/// <summary>One iOS permission and its per-locale usage descriptions.</summary>
	[Serializable]
	public sealed class PermissionUsageRow
	{
		/// <summary>The permission this row's usage descriptions apply to.</summary>
		[SerializeField] public AppPermission Permission;

		/// <summary>Per-locale usage-description values for <see cref="Permission"/>.</summary>
		[SerializeField] public List<LocaleEntry> Entries = new List<LocaleEntry> { new LocaleEntry() };
	}

	/// <summary>Per-locale values for the App Tracking Transparency (<c>NSUserTrackingUsageDescription</c>) prompt.</summary>
	[Serializable]
	public sealed class AttUsageRow
	{
		/// <summary>Per-locale ATT usage-description values.</summary>
		[SerializeField] public List<LocaleEntry> Entries = new List<LocaleEntry> { new LocaleEntry() };
	}

	/// <summary>iOS capabilities to enable in the post-build Xcode project (Info.plist + entitlements).</summary>
	[Serializable]
	public sealed class CapabilityToggles
	{
		/// <summary>Adds the Push Notifications capability/entitlement.</summary>
		[SerializeField] public bool PushNotifications;

		/// <summary>Marks App Tracking Transparency as used (drives the ATT usage-description requirement).</summary>
		[SerializeField] public bool AppTracking;

		/// <summary>Adds the Associated Domains capability (uses <see cref="AssociatedDomainList"/>).</summary>
		[SerializeField] public bool AssociatedDomains;

		/// <summary>Associated-domain entries (e.g. <c>applinks:example.com</c>) when <see cref="AssociatedDomains"/> is on.</summary>
		[SerializeField] public List<string> AssociatedDomainList = new List<string>();
	}

	/// <summary>Android <c>&lt;uses-permission&gt;</c> / <c>&lt;queries&gt;</c> entries to inject into the manifest template.</summary>
	[Serializable]
	public sealed class AndroidManifestToggles
	{
		/// <summary>Adds <c>READ_MEDIA_IMAGES</c> (Photo Library on API 33+).</summary>
		[SerializeField] public bool ReadMediaImages;

		/// <summary>Adds <c>POST_NOTIFICATIONS</c> (API 33+).</summary>
		[SerializeField] public bool PostNotifications;

		/// <summary>Adds <c>VIBRATE</c> for haptic feedback and vibrating notification channels.</summary>
		[SerializeField] public bool Vibrate;

		/// <summary>Adds <c>RECORD_AUDIO</c> (Microphone).</summary>
		[SerializeField] public bool RecordAudio;

		/// <summary>Adds <c>CAMERA</c>.</summary>
		[SerializeField] public bool Camera;

		/// <summary>Adds <c>ACCESS_FINE_LOCATION</c>.</summary>
		[SerializeField] public bool AccessFineLocation;

		/// <summary>Adds the share-chooser <c>&lt;queries&gt;</c> block (Android 11+ visibility for share targets).</summary>
		[SerializeField] public bool IncludeShareQueriesBlock;
	}

	/// <summary>
	/// Editor-only configuration asset for the Mobile Services build pipeline (iOS Info.plist /
	/// entitlements / capabilities, Android manifest + gradle, and editor-tooling toggles). Replaces
	/// the former <c>ScriptableSingleton</c> in <c>ProjectSettings/</c>: it is a regular
	/// <see cref="ScriptableObject"/> asset so it edits in the Inspector with the normal serialized-field
	/// UX (including the per-locale usage-description lists), and a single instance is located ANYWHERE
	/// in the project via <see cref="Instance"/> / <see cref="GetOrCreateAsset"/>. Open it from
	/// <c>Tools &gt; GameLovers &gt; Mobile Services &gt; Select Mobile Services Config</c>.
	/// See <c>docs/build-pipeline.md</c> for the schema and behaviour.
	/// </summary>
	/// <remarks>
	/// The type lives in the Editor assembly, so the asset is editor-only (keep it under an
	/// <c>Editor/</c> folder; it is never included in player builds). The runtime never reads it.
	/// </remarks>
	public sealed class MobileServicesConfig : ScriptableObject
	{
		/// <summary>Default/base locale code. Its value is written to the <c>Info.plist</c> root as the fallback.</summary>
		public const string DefaultLocaleCode = "en";

		/// <summary>Default Maven coordinate for the Play In-App Review library injected on Android builds.</summary>
		public const string DefaultPlayReviewCoordinate = "com.google.android.play:review:2.0.2";

		private static MobileServicesConfig _cached;
		private static bool _cachedIsTransient;

		[SerializeField] private List<PermissionUsageRow> _permissionDescriptions = new List<PermissionUsageRow>();
		[SerializeField] private AttUsageRow _attUsageDescription = new AttUsageRow();
		[SerializeField] private CapabilityToggles _capabilities = new CapabilityToggles();
		[SerializeField] private AndroidManifestToggles _androidManifest = new AndroidManifestToggles();
		[SerializeField] private bool _includePlayReviewDependency = true;
		[SerializeField] private string _playReviewDependencyCoordinate = DefaultPlayReviewCoordinate;
		[SerializeField] private bool _manageNativeBuildManually;

		/// <summary>
		/// The single config for the project, located anywhere via <see cref="AssetDatabase.FindAssets(string)"/>.
		/// If no asset exists yet, returns a transient in-memory instance carrying the defaults (so build /
		/// test reads never throw) — create a persisted asset via the
		/// <c>Tools &gt; GameLovers &gt; Mobile Services &gt; Select Mobile Services Config</c> menu.
		/// </summary>
		public static MobileServicesConfig Instance
		{
			get
			{
				if (_cached != null && !_cachedIsTransient)
				{
					return _cached;
				}

				var found = FindAsset();
				if (found != null)
				{
					_cached = found;
					_cachedIsTransient = false;
					return _cached;
				}

				if (_cached == null)
				{
					_cached = CreateInstance<MobileServicesConfig>();
					_cached.hideFlags = HideFlags.DontSave;
					_cachedIsTransient = true;
				}
				return _cached;
			}
		}

		/// <summary>Per-permission usage description rows. Reads as read-only; mutate via the explicit helpers.</summary>
		public IReadOnlyList<PermissionUsageRow> PermissionDescriptions => _permissionDescriptions;

		/// <summary>ATT (`NSUserTrackingUsageDescription`) per-locale row.</summary>
		public AttUsageRow AttUsageDescription => _attUsageDescription;

		/// <summary>iOS capability toggles (Info.plist keys + entitlements) applied at build time.</summary>
		public CapabilityToggles Capabilities => _capabilities;

		/// <summary>Android manifest permission / queries toggles injected into the manifest template at build time.</summary>
		public AndroidManifestToggles AndroidManifest => _androidManifest;

		/// <summary>
		/// When <c>true</c> (the default), the Android build postprocessor auto-injects the Play In-App
		/// Review dependency (<see cref="PlayReviewDependencyCoordinate"/>) into the generated Gradle
		/// project so <c>NativeUiService.RequestReview()</c> works with zero manual setup. It is a no-op
		/// if the dependency is already declared by any source (hand-written gradle, EDM4U, another SDK).
		/// Opt out for non-Play targets (Amazon / Huawei / sideload) or dependency-policy conflicts and
		/// add it yourself.
		/// </summary>
		public bool IncludePlayReviewDependency
		{
			get => _includePlayReviewDependency;
			set
			{
				_includePlayReviewDependency = value;
				Persist();
			}
		}

		/// <summary>
		/// The full <c>group:artifact:version</c> Maven coordinate injected for Play In-App Review.
		/// Editable so a consumer can repoint to an internal mirror, a pinned/forced version, or a
		/// variant to resolve a Gradle version conflict. Falls back to
		/// <see cref="DefaultPlayReviewCoordinate"/> when blank.
		/// </summary>
		public string PlayReviewDependencyCoordinate
		{
			get => string.IsNullOrWhiteSpace(_playReviewDependencyCoordinate)
				? DefaultPlayReviewCoordinate
				: _playReviewDependencyCoordinate;
			set
			{
				_playReviewDependencyCoordinate = value;
				Persist();
			}
		}

		/// <summary>
		/// When <c>true</c>, the package performs NO native build configuration — it writes nothing to
		/// the iOS Info.plist / entitlements / capabilities or the Android manifest / gradle, and skips
		/// the fail-fast iOS usage-description validation. Turn it on when your team manages the native
		/// build (Xcode / Gradle) yourself or via another build tool. Default OFF (the package
		/// auto-configures the build). Note: this is BUILD configuration, unrelated to render
		/// post-processing.
		/// </summary>
		public bool ManageNativeBuildManually
		{
			get => _manageNativeBuildManually;
			set
			{
				_manageNativeBuildManually = value;
				Persist();
			}
		}

		/// <summary>
		/// Finds the existing config asset anywhere in the project, or creates one under
		/// <c>Assets/Editor/</c> if none exists, then caches and returns it. Used by the
		/// <c>Select Mobile Services Config</c> menu item.
		/// </summary>
		public static MobileServicesConfig GetOrCreateAsset()
		{
			var found = FindAsset();
			if (found != null)
			{
				_cached = found;
				_cachedIsTransient = false;
				return found;
			}

			const string editorFolder = "Assets/Editor";
			if (!AssetDatabase.IsValidFolder(editorFolder))
			{
				AssetDatabase.CreateFolder("Assets", "Editor");
			}

			var created = CreateInstance<MobileServicesConfig>();
			AssetDatabase.CreateAsset(created, $"{editorFolder}/{nameof(MobileServicesConfig)}.asset");
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			_cached = created;
			_cachedIsTransient = false;
			return created;
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

		/// <summary>Returns a suggested ATT usage description following Apple's review guidelines.</summary>
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

		/// <summary>Marks the asset dirty and saves it (no-op for the transient in-memory fallback instance).</summary>
		public void Persist()
		{
			EditorUtility.SetDirty(this);
			if (AssetDatabase.Contains(this))
			{
				AssetDatabase.SaveAssets();
			}
		}

		/// <summary>
		/// Returns the row for <paramref name="permission"/>, creating a fresh one with a default
		/// locale entry if none exists yet.
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
			Persist();
			return newRow;
		}

		/// <summary>Sets the usage description for <paramref name="permission"/> in <paramref name="locale"/>.</summary>
		public void SetUsageDescription(AppPermission permission, string locale, string text)
		{
			var row = GetOrAddRow(permission);
			SetLocaleEntry(row.Entries, locale, text);
			Persist();
		}

		/// <summary>Sets the English usage description for the given permission. Convenience wrapper.</summary>
		public void SetUsageDescriptionEn(AppPermission permission, string text) =>
			SetUsageDescription(permission, DefaultLocaleCode, text);

		/// <summary>Sets the ATT usage description in <paramref name="locale"/>.</summary>
		public void SetAttUsageDescription(string locale, string text)
		{
			SetLocaleEntry(_attUsageDescription.Entries, locale, text);
			Persist();
		}

		/// <summary>Sets the English ATT usage description. Convenience wrapper.</summary>
		public void SetAttUsageDescriptionEn(string text) => SetAttUsageDescription(DefaultLocaleCode, text);

		/// <summary>Reads the usage description for <paramref name="permission"/> in <paramref name="locale"/> (null if unset).</summary>
		public string GetUsageDescription(AppPermission permission, string locale)
		{
			foreach (var row in _permissionDescriptions)
			{
				if (row.Permission != permission) continue;
				return GetLocaleEntry(row.Entries, locale);
			}
			return null;
		}

		/// <summary>Reads the English usage description for the given permission (null if unset).</summary>
		public string GetUsageDescriptionEn(AppPermission permission) =>
			GetUsageDescription(permission, DefaultLocaleCode);

		/// <summary>Reads the ATT usage description in <paramref name="locale"/> (null if unset).</summary>
		public string GetAttUsageDescription(string locale) => GetLocaleEntry(_attUsageDescription.Entries, locale);

		/// <summary>Reads the English ATT usage description (null if unset).</summary>
		public string GetAttUsageDescriptionEn() => GetAttUsageDescription(DefaultLocaleCode);

		/// <summary>
		/// Every distinct, non-empty locale code configured across all permission rows + ATT, EXCLUDING
		/// the <see cref="DefaultLocaleCode"/> base (which is written to the Info.plist root). Used by the
		/// build postprocessor to decide which <c>&lt;locale&gt;.lproj/InfoPlist.strings</c> files to emit.
		/// </summary>
		public IReadOnlyList<string> GetNonDefaultLocaleCodes()
		{
			var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var row in _permissionDescriptions)
			{
				CollectLocales(row.Entries, set);
			}
			CollectLocales(_attUsageDescription.Entries, set);
			set.RemoveWhere(code => string.Equals(code, DefaultLocaleCode, StringComparison.OrdinalIgnoreCase));
			return new List<string>(set);
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

		private static MobileServicesConfig FindAsset()
		{
			var guids = AssetDatabase.FindAssets($"t:{nameof(MobileServicesConfig)}");
			if (guids.Length == 0)
			{
				return null;
			}
			if (guids.Length > 1)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] Multiple {nameof(MobileServicesConfig)} assets found — using " +
					$"'{AssetDatabase.GUIDToAssetPath(guids[0])}'. Keep a single config asset to avoid ambiguity.");
			}
			return AssetDatabase.LoadAssetAtPath<MobileServicesConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
		}

		private static void CollectLocales(List<LocaleEntry> entries, HashSet<string> set)
		{
			foreach (var entry in entries)
			{
				if (entry != null && !string.IsNullOrWhiteSpace(entry.LocaleCode) && !string.IsNullOrWhiteSpace(entry.UsageDescription))
				{
					set.Add(entry.LocaleCode.Trim());
				}
			}
		}

		private static string GetLocaleEntry(List<LocaleEntry> entries, string locale)
		{
			foreach (var entry in entries)
			{
				if (string.Equals(entry.LocaleCode, locale, StringComparison.OrdinalIgnoreCase))
				{
					return entry.UsageDescription;
				}
			}
			return null;
		}

		private static void SetLocaleEntry(List<LocaleEntry> entries, string locale, string text)
		{
			foreach (var entry in entries)
			{
				if (string.Equals(entry.LocaleCode, locale, StringComparison.OrdinalIgnoreCase))
				{
					entry.UsageDescription = text;
					return;
				}
			}
			entries.Add(new LocaleEntry { LocaleCode = locale, UsageDescription = text });
		}
	}
}
