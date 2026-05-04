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
		public void IsSupported_AlwaysFalse()
		{
			Assert.IsFalse(_backend.IsSupported);
		}

		[Test]
		public void PlayPresetOneShot_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] PlayPresetOneShot(Selection)");
			_backend.PlayPresetOneShot(HapticPreset.Selection);
		}

		[Test]
		public void PlayPresetLoop_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] PlayPresetLoop(Warning)");
			_backend.PlayPresetLoop(HapticPreset.Warning);
		}

		[Test]
		public void PlayCustom_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] PlayCustom(intensity=0.42, durationMs=250)");
			_backend.PlayCustom(0.42f, 250f);
		}

		[Test]
		public void Stop_LogsExpected()
		{
			LogAssert.Expect(LogType.Log, "[Haptics] Stop");
			_backend.Stop();
		}
	}
}
