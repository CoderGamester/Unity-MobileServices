using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device.Internal
{
	/// <summary>Reads the running Android API level once; reports 0 when the query is unavailable.</summary>
	internal static class AndroidApiLevel
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		private static int _current = -1;

		public static int Current
		{
			get
			{
				if (_current >= 0) return _current;
				try
				{
					using var version = new AndroidJavaClass("android.os.Build$VERSION");
					_current = version.GetStatic<int>("SDK_INT");
				}
				catch
				{
					_current = 0;
				}
				return _current;
			}
		}
#else
		public static int Current => 0;
#endif
	}
}
