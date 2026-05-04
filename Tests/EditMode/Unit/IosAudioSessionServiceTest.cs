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
		public void ConfigureForPlayback_InEditor_LogsAndDoesNotThrow()
		{
			LogAssert.Expect(LogType.Log,
				"[GameLovers.MobileServices] IosAudioSessionService.ConfigureForPlayback skipped (not running on iOS device)");

			var service = new IosAudioSessionService();

			Assert.DoesNotThrow(service.ConfigureForPlayback);
		}
	}
}
