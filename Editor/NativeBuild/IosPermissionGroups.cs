// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.NativeBuild
{
	/// <summary>The iOS permission groups of <c>Plugins/iOS/Permissions.m</c> and the Info.plist keys that declare each.</summary>
	internal static class IosPermissionGroups
	{
		/// <summary>Each define gates one group in <c>Permissions.m</c>; the group compiles when any plist key is present.</summary>
		public static readonly (string Define, string Name, string[] PlistKeys)[] All =
		{
			("GL_PERMISSION_CAMERA", "camera", new[] { "NSCameraUsageDescription" }),
			("GL_PERMISSION_MICROPHONE", "microphone", new[] { "NSMicrophoneUsageDescription" }),
			("GL_PERMISSION_LOCATION", "location", new[] { "NSLocationWhenInUseUsageDescription", "NSLocationAlwaysAndWhenInUseUsageDescription" }),
			("GL_PERMISSION_PHOTOS", "photos", new[] { "NSPhotoLibraryUsageDescription", "NSPhotoLibraryAddUsageDescription" }),
		};
	}
}
