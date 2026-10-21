#ifndef GLPermissionGroups_h
#define GLPermissionGroups_h

// Each GL_PERMISSION_<GROUP> gates the code that references a privacy-protected iOS API. Without the build
// postprocessor every group compiles. When it sets GL_PERMISSIONS_DECLARED_ONLY it also defines GL_PERMISSION_<GROUP>=1
// for each group whose usage description is present in the final Info.plist (see IosPermissionGroups.cs), so the
// binary references no API that App Store validation would flag as missing its purpose string.
#ifndef GL_PERMISSIONS_DECLARED_ONLY
#define GL_PERMISSION_CAMERA 1
#define GL_PERMISSION_MICROPHONE 1
#define GL_PERMISSION_LOCATION 1
#define GL_PERMISSION_PHOTOS 1
#define GL_PERMISSION_PHOTOS_ADD 1
#endif

#endif
