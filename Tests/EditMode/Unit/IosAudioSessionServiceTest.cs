using GameLovers.MobileServices.Device;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class IosAudioSessionServiceTest
	{
		[Test]
		// ADMIT: IosAudioSessionService.ConfigureForPlayback could change the non-iOS diagnostic it emits, the only signal the call was a no-op.
		// RCR: IosAudioSessionService.cs ConfigureForPlayback — non-iOS log text `(not running on iOS device)` → `(no iOS device)` → RED (LogAssert expected message not received).
		public void ConfigureForPlayback_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log,
				"[GameLovers.MobileServices] IosAudioSessionService.ConfigureForPlayback skipped (not running on iOS device)");

			var service = new IosAudioSessionService();

			Assert.DoesNotThrow(service.ConfigureForPlayback);
		}
	}
}
