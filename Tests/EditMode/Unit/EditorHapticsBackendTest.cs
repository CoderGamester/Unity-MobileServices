using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class EditorHapticsBackendTest
	{
		private EditorHapticsBackend _backend;

		[SetUp]
		public void Init()
		{
			_backend = new EditorHapticsBackend();
		}

		[Test]
		// ADMIT: EditorHapticsBackend could claim haptic support in the Editor, so capability-gated caller code takes the device path.
		// RCR: EditorHapticsBackend.cs IsSupported — `=> false` → `=> true` → RED (expected False was True). Also reddens HapticsServiceTest.DefaultCtor_InEditor_SelectsEditorBackend_NotSupported.
		public void IsSupported_AlwaysFalse()
		{
			Assert.IsFalse(_backend.IsSupported);
		}

		[Test]
		// ADMIT: EditorHapticsBackend.PlayPresetOneShot could change its console format, breaking the Editor's only haptic feedback signal.
		// RCR: EditorHapticsBackend.cs PlayPresetOneShot — log text `PlayPresetOneShot(` → `PlayOneShot(` → RED (LogAssert expected message not received).
		public void PlayPresetOneShot_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] PlayPresetOneShot(Selection)");
			_backend.PlayPresetOneShot(HapticPreset.Selection);
		}

		[Test]
		// ADMIT: EditorHapticsBackend.PlayPresetLoop could change its console format, breaking the Editor's only haptic feedback signal.
		// RCR: EditorHapticsBackend.cs PlayPresetLoop — log text `PlayPresetLoop({preset})` → `PlayPresetLoop[{preset}]` → RED (LogAssert expected message not received).
		public void PlayPresetLoop_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] PlayPresetLoop(Warning)");
			_backend.PlayPresetLoop(HapticPreset.Warning);
		}

		[Test]
		// ADMIT: EditorHapticsBackend.PlayCustom could lose intensity precision in its log, hiding clamp/scale bugs during development.
		// RCR: EditorHapticsBackend.cs PlayCustom — intensity format `0.00` → `0.0` → RED (LogAssert expected 'intensity=0.42', got 'intensity=0.4').
		public void PlayCustom_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] PlayCustom(intensity=0.42, durationMs=250)");
			_backend.PlayCustom(0.42f, 250f);
		}

		[Test]
		// ADMIT: EditorHapticsBackend.Stop could change its console format, breaking the Editor's only haptic feedback signal.
		// RCR: EditorHapticsBackend.cs Stop — log text `[Haptics] Stop` → `[Haptics] Stopped` → RED (LogAssert expected message not received).
		public void Stop_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] Stop");
			_backend.Stop();
		}
	}
}
