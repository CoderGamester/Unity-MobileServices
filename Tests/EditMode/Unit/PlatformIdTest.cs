using GameLovers.MobileServices.GamePlatform;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class PlatformIdTest
	{
		private static readonly PlatformId Both = new PlatformId("ios.board", "android.board");

		[Test]
		// ADMIT: PlatformId.Resolve could return the Game Center id on Android or the Play Games id on iOS, so every call targets a missing leaderboard.
		// RCR: IGamePlatformServices.cs Resolve — iOS returns the Android id → RED (Expected string length 9 but was 13. Strings differ at index 0. Expected: "ios.board" B...). 2026-10-21
		[TestCase(RuntimePlatform.IPhonePlayer, "ios.board")]
		[TestCase(RuntimePlatform.Android, "android.board")]
		public void Resolve_MobilePlatform_ReturnsThatPlatformsId(RuntimePlatform platform, string expected)
		{
			Assert.AreEqual(expected, Both.Resolve(platform));
		}

		[Test]
		// ADMIT: PlatformId.Resolve could return null outside iOS and Android, so the Editor simulation gets no id at all.
		// RCR: IGamePlatformServices.cs Resolve — default branch returns null → RED (Expected: "ios.board" But was: null). 2026-10-21
		public void Resolve_Editor_FallsBackToWhicheverIdIsSet()
		{
			Assert.AreEqual("ios.board", Both.Resolve(RuntimePlatform.OSXEditor));
			Assert.AreEqual("android.only", new PlatformId(null, "android.only").Resolve(RuntimePlatform.OSXEditor));
		}
	}
}
