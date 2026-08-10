using System;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public class IosAudioSessionService : IIosAudioSessionService
	{
#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void _SetAudioSessionPlayback();
#endif

		/// <inheritdoc />
		public void ConfigureForPlayback()
		{
#if UNITY_IOS && !UNITY_EDITOR
			try
			{
				_SetAudioSessionPlayback();
				Debug.Log("[GameLovers.MobileServices] iOS audio session configured for playback");
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameLovers.MobileServices] Failed to configure iOS audio session: {e.Message}");
			}
#else
			Debug.Log("[GameLovers.MobileServices] IosAudioSessionService.ConfigureForPlayback skipped (not running on iOS device)");
#endif
		}
	}
}
