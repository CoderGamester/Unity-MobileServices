using GameLovers.MobileServices.Haptics;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class HapticPresetTest
	{
		[Test]
		// ADMIT: HapticPreset.None could stop being 0, so `default(HapticPreset)` would name a real preset and fire an unintended haptic.
		// RCR: HapticPreset.cs HapticPreset.None — `None = 0` → `None = 10` → RED (expected 0 was 10).
		public void None_IsZero()
		{
			Assert.AreEqual(0, (int) HapticPreset.None);
		}
	}
}
