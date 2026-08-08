using System;
using GameLovers.MobileServices.Editor.Settings;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.NativeBuild
{
	/// <summary>Provides a temporary, non-serialized Mobile Services configuration for one synchronous build.</summary>
	public static class MobileServicesBuildContext
	{
		private static MobileServicesConfig _activeConfig;
		private static string _activeIdentity;

		/// <summary>Returns the active build override or the project's normal configuration.</summary>
		public static MobileServicesConfig EffectiveConfig => _activeConfig != null ? _activeConfig : MobileServicesConfig.Instance;

		/// <summary>Identifies the active override, or returns an empty string outside a scoped build.</summary>
		public static string ActiveIdentity => _activeIdentity ?? string.Empty;

		/// <summary>Pushes one configured clone for the lifetime of the returned scope.</summary>
		public static IDisposable Push(string identity, Action<MobileServicesConfig> configure)
		{
			if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("A build-context identity is required.", nameof(identity));
			if (configure == null) throw new ArgumentNullException(nameof(configure));
			if (_activeConfig != null) throw new InvalidOperationException($"Mobile Services build context '{_activeIdentity}' is already active.");

			var clone = UnityEngine.Object.Instantiate(MobileServicesConfig.Instance);
			clone.name = $"{nameof(MobileServicesConfig)} ({identity})";
			clone.hideFlags = HideFlags.HideAndDontSave;
			try
			{
				configure(clone);
			}
			catch
			{
				UnityEngine.Object.DestroyImmediate(clone);
				throw;
			}

			_activeIdentity = identity;
			_activeConfig = clone;
			return new Scope(clone);
		}

		private static void Release(MobileServicesConfig clone)
		{
			if (!ReferenceEquals(_activeConfig, clone)) return;
			_activeConfig = null;
			_activeIdentity = null;
			if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
		}

		private sealed class Scope : IDisposable
		{
			private MobileServicesConfig _clone;

			public Scope(MobileServicesConfig clone)
			{
				_clone = clone;
			}

			public void Dispose()
			{
				if (_clone == null) return;
				var clone = _clone;
				_clone = null;
				Release(clone);
			}
		}
	}
}
