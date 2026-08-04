using System;
using System.Runtime.InteropServices;
using GameLovers.MobileServices.Device.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <inheritdoc />
	public sealed class BatteryService : IBatteryService, IDisposable
	{
#if UNITY_IOS && !UNITY_EDITOR
		[DllImport("__Internal")] private static extern bool _GameLoversBatteryIsLowPowerModeEnabled();
		[DllImport("__Internal")] private static extern void _GameLoversBatteryStartObservingLowPowerMode();
		[DllImport("__Internal")] private static extern void _GameLoversBatteryStopObservingLowPowerMode();
#endif

		private const float LevelChangeThreshold = 0.01f;

		private readonly DeviceServicesHost _host;

		private float _lastLevel;
		private BatteryStatus _lastStatus;
		private bool _lastLowPowerMode;

		/// <inheritdoc />
		public float Level => SystemInfo.batteryLevel;

		/// <inheritdoc />
		public BatteryStatus Status => SystemInfo.batteryStatus;

		/// <inheritdoc />
		public bool IsLowPowerMode { get; private set; }

		/// <inheritdoc />
		public event Action OnLevelChanged;
		/// <inheritdoc />
		public event Action OnStatusChanged;
		/// <inheritdoc />
		public event Action OnLowPowerModeChanged;

		public BatteryService() : this(DeviceServicesHost.Instance) { }

		internal BatteryService(DeviceServicesHost host)
		{
			_host = host;
			_lastLevel = Level;
			_lastStatus = Status;
			IsLowPowerMode = QueryLowPowerMode();
			_lastLowPowerMode = IsLowPowerMode;

			_host.RegisterSecondTick(OnSecondTick);
			_host.RegisterFocusChanged(OnFocusChanged);
			_host.RegisterIosLowPowerModeChanged(OnIosLowPowerModeChanged);

#if UNITY_IOS && !UNITY_EDITOR
			_GameLoversBatteryStartObservingLowPowerMode();
#endif
		}

		/// <inheritdoc />
		public void Dispose()
		{
			_host.UnregisterSecondTick(OnSecondTick);
			_host.UnregisterFocusChanged(OnFocusChanged);
			_host.UnregisterIosLowPowerModeChanged(OnIosLowPowerModeChanged);

#if UNITY_IOS && !UNITY_EDITOR
			_GameLoversBatteryStopObservingLowPowerMode();
#endif
		}

#if UNITY_EDITOR
		/// <summary>
		/// Editor-only test/simulator hook. Runs the LPM refresh path that would normally be
		/// driven by the iOS bridge (<c>UnitySendMessage("DeviceServicesHost", "OnIosLowPowerModeChanged", "")</c>),
		/// re-reading <see cref="EditorLowPowerModeOverride"/> and firing
		/// <see cref="OnLowPowerModeChanged"/> on transition.
		/// </summary>
		internal void SimulateLowPowerModeChanged()
		{
			RefreshLowPowerMode();
		}
#endif

		private void OnSecondTick()
		{
			var current = Level;
			if (Mathf.Abs(current - _lastLevel) >= LevelChangeThreshold)
			{
				_lastLevel = current;
				OnLevelChanged?.Invoke();
			}

			var status = Status;
			if (status != _lastStatus)
			{
				_lastStatus = status;
				OnStatusChanged?.Invoke();
			}
		}

		private void OnFocusChanged(bool focused)
		{
			if (!focused)
			{
				return;
			}
			RefreshLowPowerMode();
		}

		private void OnIosLowPowerModeChanged()
		{
			RefreshLowPowerMode();
		}

		private void RefreshLowPowerMode()
		{
			var current = QueryLowPowerMode();
			if (current == _lastLowPowerMode)
			{
				return;
			}
			_lastLowPowerMode = current;
			IsLowPowerMode = current;
			OnLowPowerModeChanged?.Invoke();
		}

		private static bool QueryLowPowerMode()
		{
#if UNITY_IOS && !UNITY_EDITOR
			try
			{
				return _GameLoversBatteryIsLowPowerModeEnabled();
			}
			catch
			{
				return false;
			}
#elif UNITY_ANDROID && !UNITY_EDITOR
			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var powerManager = activity.Call<AndroidJavaObject>("getSystemService", "power");
				return powerManager.Call<bool>("isPowerSaveMode");
			}
			catch
			{
				return false;
			}
#elif UNITY_EDITOR
			return EditorLowPowerModeOverride;
#else
			return false;
#endif
		}

#if UNITY_EDITOR
		// Editor-only simulator hook. EditorPlatformSimulator flips this then calls
		// SimulateLowPowerModeChanged() to fan the change through to subscribers, mirroring
		// what NSProcessInfoPowerStateDidChangeNotification would do on iOS.
		internal static bool EditorLowPowerModeOverride;
#endif
	}
}
