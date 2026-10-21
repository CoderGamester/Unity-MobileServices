// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.NativeBuild
{
	/// <summary>The iOS permission groups of the native bridges and the Info.plist keys that declare each.</summary>
	internal static class IosPermissionGroups
	{
		/// <summary>Each define gates one native group, which compiles when any of its plist keys is present.</summary>
		public static readonly (string Define, string Name, string[] PlistKeys)[] All =
		{
			("GL_PERMISSION_CAMERA", "camera", new[] { "NSCameraUsageDescription" }),
			("GL_PERMISSION_MICROPHONE", "microphone", new[] { "NSMicrophoneUsageDescription" }),
			("GL_PERMISSION_LOCATION", "location", new[] { "NSLocationWhenInUseUsageDescription", "NSLocationAlwaysAndWhenInUseUsageDescription" }),
			("GL_PERMISSION_PHOTOS", "photos", new[] { "NSPhotoLibraryUsageDescription" }),
			("GL_PERMISSION_PHOTOS_ADD", "photos add-only", new[] { "NSPhotoLibraryAddUsageDescription" }),
		};
	}
}
