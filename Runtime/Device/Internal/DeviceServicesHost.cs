using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device.Internal
{
	/// <summary>
	/// Internal MonoBehaviour shared by all event-driven Device services (SafeArea, Battery).
	/// Spawned lazily on first use, marked <c>DontDestroyOnLoad</c>. Exists so the runtime cost of the
	/// Device subsystem is one auto-spawned GameObject instead of one per service.
	/// </summary>
	/// <remarks>
	/// Although every member of this class is logically <c>internal</c>, the
	/// <see cref="OnIosLowPowerModeChanged"/> entry point is kept <c>public</c> because it is reached
	/// by Unity's <c>UnitySendMessage</c> dispatcher from the iOS native bridge
	/// (<c>Plugins/iOS/Battery.m</c>). Keeping it explicitly <c>public</c> documents that contract
	/// and avoids any future change to Unity's reflection visibility defaults breaking the bridge.
	/// </remarks>
	internal sealed class DeviceServicesHost : MonoBehaviour
	{
		// Per-service callback registration. Each callback is invoked once per Update at most;
		// each service is responsible for diffing and firing its own events from the callback.
		private event Action _onLateUpdate;
		private event Action _onSecondTick;
		private event Action<bool> _onApplicationFocusChanged;
		private event Action _onIosLowPowerModeChanged;

		private float _secondAccumulator;

		private static DeviceServicesHost _instance;

		/// <summary>Returns the shared instance, spawning the host GameObject on first call.</summary>
		internal static DeviceServicesHost Instance
		{
			get
			{
				if (_instance != null)
				{
					return _instance;
				}

				var go = new GameObject("DeviceServicesHost");
				DontDestroyOnLoad(go);
				_instance = go.AddComponent<DeviceServicesHost>();
				return _instance;
			}
		}

		/// <summary>
		/// Tears down the singleton: destroys the GameObject (if any) and clears the static reference.
		/// Intended for EditMode tests that need a clean state between runs.
		/// </summary>
		internal static void ResetForTests()
		{
			if (_instance == null)
			{
				return;
			}

			var go = _instance.gameObject;
			_instance = null;

			if (Application.isPlaying)
			{
				Destroy(go);
			}
			else
			{
				DestroyImmediate(go);
			}
		}

		/// <summary>Subscribes to a per-LateUpdate poll tick.</summary>
		internal void RegisterLateUpdate(Action callback)
		{
			_onLateUpdate += callback;
		}

		/// <summary>Unsubscribes from the LateUpdate poll tick.</summary>
		internal void UnregisterLateUpdate(Action callback)
		{
			_onLateUpdate -= callback;
		}

		/// <summary>Subscribes to a roughly-once-per-second tick (cheaper than LateUpdate; useful for connectivity polling).</summary>
		internal void RegisterSecondTick(Action callback)
		{
			_onSecondTick += callback;
		}

		/// <summary>Unsubscribes from the once-per-second tick.</summary>
		internal void UnregisterSecondTick(Action callback)
		{
			_onSecondTick -= callback;
		}

		/// <summary>Subscribes to <c>OnApplicationFocus(bool focused)</c>.</summary>
		internal void RegisterFocusChanged(Action<bool> callback)
		{
			_onApplicationFocusChanged += callback;
		}

		/// <summary>Unsubscribes from the application-focus signal.</summary>
		internal void UnregisterFocusChanged(Action<bool> callback)
		{
			_onApplicationFocusChanged -= callback;
		}

		/// <summary>Subscribes to the iOS low-power-mode change signal sourced from the native bridge.</summary>
		internal void RegisterIosLowPowerModeChanged(Action callback)
		{
			_onIosLowPowerModeChanged += callback;
		}

		/// <summary>Unsubscribes from the iOS low-power-mode signal.</summary>
		internal void UnregisterIosLowPowerModeChanged(Action callback)
		{
			_onIosLowPowerModeChanged -= callback;
		}

		// MUST stay public: invoked by Unity's UnitySendMessage from Plugins/iOS/Battery.m as
		// UnitySendMessage("DeviceServicesHost", "OnIosLowPowerModeChanged", "").
		// Renaming this method or its enclosing GameObject requires updating the iOS .m file.
		// ReSharper disable once UnusedMember.Global
		// ReSharper disable once InconsistentNaming
		/// <summary>
		/// Fans the iOS low-power-mode change out to subscribers. Must stay public and keep this name:
		/// the native bridge dispatches it by string through Unity.
		/// </summary>
		public void OnIosLowPowerModeChanged(string _)
		{
			_onIosLowPowerModeChanged?.Invoke();
		}

		private void LateUpdate()
		{
			_onLateUpdate?.Invoke();

			_secondAccumulator += Time.unscaledDeltaTime;
			if (_secondAccumulator >= 1f)
			{
				_secondAccumulator = 0f;
				_onSecondTick?.Invoke();
			}
		}

		private void OnApplicationFocus(bool focused)
		{
			_onApplicationFocusChanged?.Invoke(focused);
		}

		private void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}
	}
}
