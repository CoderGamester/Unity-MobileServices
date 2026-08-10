using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class NoOpHapticsBackendTest
	{
		[Test]
		// ADMIT: NoOpHapticsBackend could claim haptic support on a platform where every call is a no-op.
		// RCR: NoOpHapticsBackend.cs IsSupported — `=> false` → `=> true` → RED (expected False was True).
		public void AllMembers_DoNotThrow_AndIsSupportedFalse()
		{
			var backend = new NoOpHapticsBackend();

			Assert.IsFalse(backend.IsSupported);
			Assert.DoesNotThrow(() => backend.PlayPresetOneShot(HapticPreset.Success));
			Assert.DoesNotThrow(() => backend.PlayPresetLoop(HapticPreset.Selection));
			Assert.DoesNotThrow(() => backend.PlayCustom(0.5f, 100f));
			Assert.DoesNotThrow(backend.Stop);
		}
	}
}
