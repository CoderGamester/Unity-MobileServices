using GameLovers.MobileServices.Haptics;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class HapticPresetTest
	{
		[Test]
		public void None_IsZero()
		{
			Assert.AreEqual(0, (int) HapticPreset.None);
		}
	}
}
