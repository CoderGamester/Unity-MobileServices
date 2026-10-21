// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.NativeBuild
{
	/// <summary>The iOS permission groups of the <c>Plugins/iOS</c> bridges (see <c>GLPermissionGroups.h</c>) and the Info.plist keys that declare each.</summary>
	internal static class IosPermissionGroups
	{
		/// <summary>Each define gates one group in the native bridges; the group compiles when any plist key is present.</summary>
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
