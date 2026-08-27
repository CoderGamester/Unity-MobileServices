using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Haptics.Internal;
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

	/// <summary>
	/// Per-locale values for the App Tracking Transparency
	/// (<c>NSUserTrackingUsageDescription</c>) prompt.
	/// </summary>
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

		/// <summary>
		/// Associated-domain entries (e.g. <c>applinks:example.com</c>) when
		/// <see cref="AssociatedDomains"/> is on.
		/// </summary>
		[SerializeField] public List<string> AssociatedDomainList = new List<string>();
	}

	/// <summary>
	/// Android <c>&lt;uses-permission&gt;</c> / <c>&lt;queries&gt;</c> entries to inject into the
	/// manifest template.
	/// </summary>
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

		/// <summary>
		/// Adds the share-chooser <c>&lt;queries&gt;</c> block (Android 11+ visibility for share targets).
		/// </summary>
		[SerializeField] public bool IncludeShareQueriesBlock;
	}

	/// <summary>One Android browsable deep-link registration.</summary>
	[Serializable]
	public sealed class AndroidDeepLinkRegistration
	{
		/// <summary>Required URI scheme.</summary>
		[SerializeField] public string Scheme;

		/// <summary>Optional host constraint.</summary>
		[SerializeField] public string Host;

		/// <summary>Optional path-prefix constraint.</summary>
		[SerializeField] public string PathPrefix;
	}

	/// <summary>Native deep-link declarations shared by production builds and temporary sample overlays.</summary>
	[Serializable]
	public sealed class NativeDeepLinkSettings
	{
		/// <summary>iOS custom URL schemes.</summary>
		[SerializeField] public List<string> IosUrlSchemes = new List<string>();

		/// <summary>Android browsable intent-filter registrations.</summary>
		[SerializeField] public List<AndroidDeepLinkRegistration> AndroidIntentFilters = new List<AndroidDeepLinkRegistration>();

		private void OnEnable()
		{
			IosUrlSchemes ??= new List<string>();
			AndroidIntentFilters ??= new List<AndroidDeepLinkRegistration>();
		}

		/// <summary>Adds an iOS scheme using case-insensitive set semantics.</summary>
		public void AddIosUrlScheme(string scheme)
		{
			IosUrlSchemes ??= new List<string>();
			scheme = Normalize(scheme);
			if (string.IsNullOrEmpty(scheme)) return;
			foreach (var existing in IosUrlSchemes)
			{
				if (string.Equals(existing?.Trim(), scheme, StringComparison.OrdinalIgnoreCase)) return;
			}
			IosUrlSchemes.Add(scheme);
		}

		/// <summary>Adds an Android registration using semantic set semantics.</summary>
		public void AddAndroidIntentFilter(string scheme, string host = null, string pathPrefix = null)
		{
			AndroidIntentFilters ??= new List<AndroidDeepLinkRegistration>();
			var registration = new AndroidDeepLinkRegistration
			{
				Scheme = Normalize(scheme),
				Host = Normalize(host),
				PathPrefix = Normalize(pathPrefix)
			};
			if (string.IsNullOrEmpty(registration.Scheme)) return;

			foreach (var existing in AndroidIntentFilters)
			{
				if (existing == null) continue;
				if (string.Equals(existing.Scheme?.Trim(), registration.Scheme, StringComparison.OrdinalIgnoreCase) &&
					string.Equals(existing.Host?.Trim(), registration.Host, StringComparison.OrdinalIgnoreCase) &&
					string.Equals(existing.PathPrefix?.Trim(), registration.PathPrefix, StringComparison.Ordinal))
				{
					return;
				}
			}
			AndroidIntentFilters.Add(registration);
		}

		/// <summary>Appends malformed deep-link declarations to the supplied error collection.</summary>
		internal void CollectValidationErrors(string prefix, List<string> errors)
		{
			if (IosUrlSchemes == null)
			{
				errors.Add($"{prefix}.IosUrlSchemes is null.");
			}
			else
			{
				for (var i = 0; i < IosUrlSchemes.Count; i++)
				{
					var scheme = IosUrlSchemes[i]?.Trim();
					if (string.IsNullOrEmpty(scheme) || Uri.CheckSchemeName(scheme) == false)
					{
						errors.Add($"{prefix}.IosUrlSchemes[{i}] has an invalid URI scheme '{IosUrlSchemes[i]}'.");
					}
				}
			}

			if (AndroidIntentFilters == null)
			{
				errors.Add($"{prefix}.AndroidIntentFilters is null.");
				return;
			}
			for (var i = 0; i < AndroidIntentFilters.Count; i++)
			{
				var entry = AndroidIntentFilters[i];
				if (entry == null)
				{
					errors.Add($"{prefix}.AndroidIntentFilters[{i}] is null.");
					continue;
				}
				if (string.IsNullOrWhiteSpace(entry.Scheme) || Uri.CheckSchemeName(entry.Scheme.Trim()) == false)
				{
					errors.Add($"{prefix}.AndroidIntentFilters[{i}].Scheme is invalid: '{entry.Scheme}'.");
				}
				if (!string.IsNullOrWhiteSpace(entry.Host) && ContainsIllegalHostCharacters(entry.Host))
				{
					errors.Add($"{prefix}.AndroidIntentFilters[{i}].Host is invalid: '{entry.Host}'.");
				}
				if (!string.IsNullOrWhiteSpace(entry.PathPrefix) &&
					(!entry.PathPrefix.Trim().StartsWith("/", StringComparison.Ordinal) || entry.PathPrefix.IndexOfAny(new[] { '?', '#' }) >= 0))
				{
					errors.Add($"{prefix}.AndroidIntentFilters[{i}].PathPrefix must start with '/' and contain no query or fragment: '{entry.PathPrefix}'.");
				}
			}
		}

		private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

		private static bool ContainsIllegalHostCharacters(string host)
		{
			foreach (var character in host.Trim())
			{
				if (char.IsWhiteSpace(character) || character == '/' || character == '?' || character == '#') return true;
			}
			return false;
		}
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
		/// <summary>
		/// Default/base locale code. Its value is written to the <c>Info.plist</c> root as the fallback.
		/// </summary>
		public const string DefaultLocaleCode = "en";

		/// <summary>Default Maven coordinate for the Play In-App Review library injected on Android builds.</summary>
		public const string DefaultPlayReviewCoordinate = "com.google.android.play:review:2.0.2";

		private static MobileServicesConfig _cached;
		private static bool _cachedIsTransient;

		[SerializeField] private List<PermissionUsageRow> _permissionDescriptions = new List<PermissionUsageRow>();
		[SerializeField] private AttUsageRow _attUsageDescription = new AttUsageRow();
		[SerializeField] private CapabilityToggles _capabilities = new CapabilityToggles();
		[SerializeField] private AndroidManifestToggles _androidManifest = new AndroidManifestToggles();
		[SerializeField] private NativeDeepLinkSettings _deepLinks = new NativeDeepLinkSettings();
		[Tooltip("Logs Editor haptics play and stop calls to the Unity console. Disabled by default.")]
		[SerializeField] private bool _enableHapticsDebugLogs;
		[SerializeField] private bool _includePlayReviewDependency = true;
		[SerializeField] private string _playReviewDependencyCoordinate = DefaultPlayReviewCoordinate;
		[SerializeField] private bool _manageNativeBuildManually;

		private void OnEnable()
		{
			_permissionDescriptions ??= new List<PermissionUsageRow>();
			_attUsageDescription ??= new AttUsageRow();
			_attUsageDescription.Entries ??= new List<LocaleEntry>();
			_capabilities ??= new CapabilityToggles();
			_capabilities.AssociatedDomainList ??= new List<string>();
			_androidManifest ??= new AndroidManifestToggles();
			_deepLinks ??= new NativeDeepLinkSettings();
			_deepLinks.IosUrlSchemes ??= new List<string>();
			_deepLinks.AndroidIntentFilters ??= new List<AndroidDeepLinkRegistration>();
			_playReviewDependencyCoordinate ??= DefaultPlayReviewCoordinate;
			ApplyEditorRuntimeSettings();
		}

		private void OnValidate()
		{
			ApplyEditorRuntimeSettings();
		}

		/// <summary>
		/// The single config for the project, located anywhere via <see cref="AssetDatabase.FindAssets(string)"/>.
		/// If no asset exists yet, returns a transient in-memory instance carrying the defaults for editor
		/// convenience only. Build callbacks use <see cref="TryGetPersistedConfig(out MobileServicesConfig)"/>
		/// and remain a no-op until a persisted asset or an explicit temporary build context exists — create a
		/// persisted asset via the
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

		/// <summary>
		/// Per-permission usage description rows. Reads as read-only; mutate via the explicit helpers.
		/// </summary>
		public IReadOnlyList<PermissionUsageRow> PermissionDescriptions => _permissionDescriptions;

		/// <summary>ATT (`NSUserTrackingUsageDescription`) per-locale row.</summary>
		public AttUsageRow AttUsageDescription => _attUsageDescription;

		/// <summary>iOS capability toggles (Info.plist keys + entitlements) applied at build time.</summary>
		public CapabilityToggles Capabilities => _capabilities;

		/// <summary>
		/// Android manifest permission / queries toggles injected into the manifest template at build time.
		/// </summary>
		public AndroidManifestToggles AndroidManifest => _androidManifest;

		/// <summary>iOS URL scheme and Android intent-filter declarations applied at build time.</summary>
		public NativeDeepLinkSettings DeepLinks => _deepLinks ??= new NativeDeepLinkSettings();

		/// <summary>Whether Editor haptics calls write diagnostic messages to the Unity console.</summary>
		public bool EnableHapticsDebugLogs
		{
			get => _enableHapticsDebugLogs;
			set
			{
				_enableHapticsDebugLogs = value;
				ApplyEditorRuntimeSettings();
				Persist();
			}
		}

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

		/// <summary>Loads the unique persisted config, returning false when no asset exists.</summary>
		public static bool TryGetPersistedConfig(out MobileServicesConfig config)
		{
			var guids = AssetDatabase.FindAssets($"t:{nameof(MobileServicesConfig)}");
			if (guids.Length == 0)
			{
				config = null;
				return false;
			}
			if (guids.Length > 1)
			{
				var paths = new List<string>();
				foreach (var guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
				throw new InvalidOperationException($"Multiple {nameof(MobileServicesConfig)} assets were found: {string.Join(", ", paths)}. Keep exactly one config asset.");
			}

			var path = AssetDatabase.GUIDToAssetPath(guids[0]);
			config = AssetDatabase.LoadAssetAtPath<MobileServicesConfig>(path);
			if (config == null) throw new InvalidOperationException($"The Mobile Services config at '{path}' could not be loaded.");
			return true;
		}

		/// <summary>Creates an unsaved configuration with every native requirement disabled.</summary>
		internal static MobileServicesConfig CreateNeutralTransient()
		{
			var config = CreateInstance<MobileServicesConfig>();
			config._includePlayReviewDependency = false;
			config.hideFlags = HideFlags.HideAndDontSave;
			return config;
		}

		/// <summary>Appends malformed explicitly configured values to the supplied error collection.</summary>
		internal void CollectValidationErrors(List<string> errors)
		{
			if (errors == null) throw new ArgumentNullException(nameof(errors));
			if (_permissionDescriptions == null)
			{
				errors.Add("PermissionDescriptions is null.");
			}
			else
			{
				var permissions = new HashSet<AppPermission>();
				for (var rowIndex = 0; rowIndex < _permissionDescriptions.Count; rowIndex++)
				{
					var row = _permissionDescriptions[rowIndex];
					if (row == null)
					{
						errors.Add($"PermissionDescriptions[{rowIndex}] is null.");
						continue;
					}
					if (!permissions.Add(row.Permission)) errors.Add($"PermissionDescriptions contains duplicate permission '{row.Permission}'.");
					CollectLocaleValidationErrors($"PermissionDescriptions[{rowIndex}].Entries", row.Entries, errors);
					if (GetIosUsageKey(row.Permission) != null && string.IsNullOrWhiteSpace(GetLocaleEntry(row.Entries, DefaultLocaleCode)))
					{
						errors.Add($"PermissionDescriptions[{rowIndex}] for '{row.Permission}' has no English usage description.");
					}
				}
			}
			if (_attUsageDescription == null) errors.Add("AttUsageDescription is null.");
			else CollectLocaleValidationErrors("AttUsageDescription.Entries", _attUsageDescription.Entries, errors);
			if (_capabilities == null) errors.Add("Capabilities is null.");
			else
			{
				if (_capabilities.AssociatedDomainList == null) errors.Add("Capabilities.AssociatedDomainList is null.");
				else if (_capabilities.AssociatedDomains)
				{
					var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					for (var i = 0; i < _capabilities.AssociatedDomainList.Count; i++)
					{
						var domain = _capabilities.AssociatedDomainList[i];
						if (string.IsNullOrWhiteSpace(domain)) errors.Add($"Capabilities.AssociatedDomainList[{i}] is empty while AssociatedDomains is enabled.");
						else if (!domains.Add(domain.Trim())) errors.Add($"Capabilities.AssociatedDomainList contains duplicate value '{domain}'.");
					}
				}
			}
			if (_androidManifest == null) errors.Add("AndroidManifest is null.");
			if (_deepLinks == null) errors.Add("DeepLinks is null.");
			if (_capabilities != null && _capabilities.AppTracking && string.IsNullOrWhiteSpace(GetAttUsageDescriptionEn()))
			{
				errors.Add("Capabilities.AppTracking is enabled but AttUsageDescription has no English value.");
			}
			if (_capabilities != null && _capabilities.AssociatedDomains && (_capabilities.AssociatedDomainList == null || _capabilities.AssociatedDomainList.Count == 0))
			{
				errors.Add("Capabilities.AssociatedDomains is enabled but AssociatedDomainList is empty.");
			}
			_deepLinks?.CollectValidationErrors(nameof(DeepLinks), errors);
			if (_includePlayReviewDependency && !IsValidPlayReviewCoordinate(PlayReviewDependencyCoordinate))
			{
				errors.Add($"PlayReviewDependencyCoordinate is invalid: '{PlayReviewDependencyCoordinate}'.");
			}
		}

		/// <summary>Validates explicitly enabled settings before a build or generated native file is touched.</summary>
		public bool TryValidate(out string error)
		{
			var errors = new List<string>();
			CollectValidationErrors(errors);
			error = errors.Count == 0 ? null : string.Join("\n- ", errors);
			return errors.Count == 0;
		}

		private static bool IsValidPlayReviewCoordinate(string coordinate)
		{
			if (string.IsNullOrWhiteSpace(coordinate)) return false;
			var pieces = coordinate.Split(':');
			return pieces.Length == 3 && Array.TrueForAll(pieces, piece => !string.IsNullOrWhiteSpace(piece));
		}

		private static void CollectLocaleValidationErrors(string prefix, List<LocaleEntry> entries, List<string> errors)
		{
			if (entries == null)
			{
				errors.Add($"{prefix} is null.");
				return;
			}

			var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (var i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				if (entry == null)
				{
					errors.Add($"{prefix}[{i}] is null.");
					continue;
				}
				var locale = entry.LocaleCode?.Trim();
				if (string.IsNullOrEmpty(locale))
				{
					errors.Add($"{prefix}[{i}].LocaleCode is empty.");
				}
				else if (!locales.Add(locale))
				{
					errors.Add($"{prefix} contains duplicate locale '{locale}'.");
				}
				if (!string.IsNullOrWhiteSpace(entry.UsageDescription) && string.IsNullOrEmpty(locale))
				{
					errors.Add($"{prefix}[{i}].UsageDescription has a value but LocaleCode is empty.");
				}
			}
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

		/// <summary>
		/// Sets the usage description for <paramref name="permission"/> in <paramref name="locale"/>.
		/// </summary>
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

		/// <summary>
		/// Reads the usage description for <paramref name="permission"/> in
		/// <paramref name="locale"/> (null if unset).
		/// </summary>
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
			foreach (var row in _permissionDescriptions ?? new List<PermissionUsageRow>())
			{
				if (row != null) CollectLocales(row.Entries, set);
			}
			if (_attUsageDescription != null) CollectLocales(_attUsageDescription.Entries, set);
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
			return TryGetPersistedConfig(out var config) ? config : null;
		}

		private void ApplyEditorRuntimeSettings()
		{
			EditorHapticsBackend.DebugLoggingEnabled = _enableHapticsDebugLogs;
		}

		private static void CollectLocales(List<LocaleEntry> entries, HashSet<string> set)
		{
			foreach (var entry in entries ?? new List<LocaleEntry>())
			{
				if (entry != null && !string.IsNullOrWhiteSpace(entry.LocaleCode) && !string.IsNullOrWhiteSpace(entry.UsageDescription))
				{
					set.Add(entry.LocaleCode.Trim());
				}
			}
		}

		private static string GetLocaleEntry(List<LocaleEntry> entries, string locale)
		{
			foreach (var entry in entries ?? new List<LocaleEntry>())
			{
				if (entry != null && string.Equals(entry.LocaleCode, locale, StringComparison.OrdinalIgnoreCase))
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

	/// <summary>Restores Mobile Services Editor runtime settings after Unity reloads the package assemblies.</summary>
	[InitializeOnLoad]
	internal static class MobileServicesConfigInitializer
	{
		static MobileServicesConfigInitializer()
		{
			EditorApplication.delayCall += Apply;
		}

		private static void Apply()
		{
			EditorHapticsBackend.DebugLoggingEnabled = MobileServicesConfig.Instance.EnableHapticsDebugLogs;
		}
	}
}
