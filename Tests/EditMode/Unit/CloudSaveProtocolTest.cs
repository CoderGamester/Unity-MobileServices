using System;
using System.Text;
using GameLovers.MobileServices.GamePlatform;
using GameLovers.MobileServices.GamePlatform.Internal;
using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class CloudSaveProtocolTest
	{
		[Test]
		// ADMIT: CloudSaveProtocol.ReadPayload could return the tag with the payload, corrupting the JSON the iOS bridge sent.
		// RCR: CloudSaveProtocol.cs ReadPayload — tag kept in the payload → RED (Expected string length 12 but was 13. Strings differ at index 0. Expected: "{"items":[]...). 2026-10-21
		public void ReadPayload_Success_StripsTheTag()
		{
			Assert.AreEqual("{\"items\":[]}", CloudSaveProtocol.ReadPayload("K{\"items\":[]}"));
			Assert.AreEqual(string.Empty, CloudSaveProtocol.ReadPayload("K"));
		}

		[Test]
		// ADMIT: CloudSaveProtocol.ReadPayload could map every native error to Io, hiding that the player is signed out or iCloud is off.
		// RCR: CloudSaveProtocol.cs ReadPayload — every error mapped to Io → RED (Expected: NotAuthenticated But was: Io). 2026-10-21
		[TestCase("ENotAuthenticated|Sign in first", CloudSaveFailure.NotAuthenticated, "Sign in first")]
		[TestCase("ECloudUnavailable|iCloud Drive is off", CloudSaveFailure.CloudUnavailable, "iCloud Drive is off")]
		[TestCase("EIo|disk full", CloudSaveFailure.Io, "disk full")]
		[TestCase("EUnknownReason|boom", CloudSaveFailure.Io, "boom")]
		public void ReadPayload_Error_ThrowsTheMatchingFailure(string reply, CloudSaveFailure reason, string message)
		{
			var exception = Assert.Throws<CloudSaveException>(() => CloudSaveProtocol.ReadPayload(reply));

			Assert.AreEqual(reason, exception.Reason);
			Assert.AreEqual(message, exception.Message);
		}

		[Test]
		// ADMIT: CloudSaveProtocol.ReadPayload could treat a missing reply as success, so a dropped callback looks like a saved game.
		// RCR: CloudSaveProtocol.cs ReadPayload — empty reply returned as success → RED (Expected: <GameLovers.MobileServices.GamePlatform.CloudSaveException> But was: null). 2026-10-21
		[TestCase(null)]
		[TestCase("")]
		[TestCase("Xwhat")]
		public void ReadPayload_MissingOrUnknownReply_ThrowsIo(string reply)
		{
			var exception = Assert.Throws<CloudSaveException>(() => CloudSaveProtocol.ReadPayload(reply));

			Assert.AreEqual(CloudSaveFailure.Io, exception.Reason);
		}

		[Test]
		// ADMIT: CloudSaveProtocol.ParseVersions could return versions oldest first or skip the base64 decode, so a conflict resolves to the wrong data.
		// RCR: CloudSaveProtocol.cs ParseVersions — sorted oldest first → RED (String lengths are both 5. Strings differ at index 0. Expected: "newer" But was: "older...). 2026-10-21
		public void ParseVersions_DecodesDataAndOrdersNewestFirst()
		{
			var older = Convert.ToBase64String(Encoding.UTF8.GetBytes("older"));
			var newer = Convert.ToBase64String(Encoding.UTF8.GetBytes("newer"));
			var json = "{\"items\":[" +
				$"{{\"name\":\"s\",\"modified\":1000,\"device\":\"phone\",\"data\":\"{older}\"}}," +
				$"{{\"name\":\"s\",\"modified\":2000,\"device\":\"tablet\",\"data\":\"{newer}\"}}]}}";

			var versions = CloudSaveProtocol.ParseVersions(json);

			Assert.AreEqual(2, versions.Count);
			Assert.AreEqual("newer", Encoding.UTF8.GetString(versions[0].Data));
			Assert.AreEqual("tablet", versions[0].DeviceName);
			Assert.AreEqual(new DateTime(1970, 1, 1, 0, 33, 20, DateTimeKind.Utc), versions[0].ModifiedUtc);
		}

		[Test]
		// ADMIT: CloudSaveProtocol.ParseSlots could drop the modification time or throw on an empty list.
		// RCR: CloudSaveProtocol.cs ParseSlots — modified time dropped → RED (Expected: 1970-01-01 00:01:00.000 But was: 0001-01-01 00:00:00.000). 2026-10-21
		public void ParseSlots_ReadsNamesAndTimes_AndToleratesAnEmptyList()
		{
			var slots = CloudSaveProtocol.ParseSlots("{\"items\":[{\"name\":\"slot1\",\"description\":\"d\",\"modified\":60,\"device\":\"phone\"}]}");

			Assert.AreEqual("slot1", slots[0].Name);
			Assert.AreEqual("d", slots[0].Description);
			Assert.AreEqual(new DateTime(1970, 1, 1, 0, 1, 0, DateTimeKind.Utc), slots[0].ModifiedUtc);
			Assert.AreEqual(0, CloudSaveProtocol.ParseSlots("{\"items\":[]}").Count);
			Assert.AreEqual(0, CloudSaveProtocol.ParseSlots(string.Empty).Count);
		}
	}
}
