using GameLovers.MobileServices.Notifications;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class GameNotificationChannelTest
	{
		[Test]
		public void Ctor_ThreeArg_AppliesDefaults()
		{
			var channel = new GameNotificationChannel("id", "name", "description");

			Assert.AreEqual("id", channel.Id);
			Assert.AreEqual("name", channel.Name);
			Assert.AreEqual("description", channel.Description);
			Assert.IsTrue(channel.ShowsBadge);
			Assert.IsFalse(channel.ShowLights);
			Assert.IsTrue(channel.Vibrates);
			Assert.IsFalse(channel.HighPriority);
			Assert.AreEqual(GameNotificationChannel.NotificationStyle.Popup, channel.Style);
			Assert.AreEqual(GameNotificationChannel.PrivacyMode.Public, channel.Privacy);
			Assert.IsNull(channel.VibrationPattern);
		}

		[Test]
		public void Ctor_FullArg_StoresAllFields()
		{
			var pattern = new long[] { 100L, 200L, 300L };

			var channel = new GameNotificationChannel(
				"id",
				"name",
				"description",
				GameNotificationChannel.NotificationStyle.NoSound,
				showsBadge: false,
				showLights: true,
				vibrates: false,
				highPriority: true,
				privacy: GameNotificationChannel.PrivacyMode.Secret,
				vibrationPattern: pattern);

			Assert.AreEqual("id", channel.Id);
			Assert.AreEqual("name", channel.Name);
			Assert.AreEqual("description", channel.Description);
			Assert.IsFalse(channel.ShowsBadge);
			Assert.IsTrue(channel.ShowLights);
			Assert.IsFalse(channel.Vibrates);
			Assert.IsTrue(channel.HighPriority);
			Assert.AreEqual(GameNotificationChannel.NotificationStyle.NoSound, channel.Style);
			Assert.AreEqual(GameNotificationChannel.PrivacyMode.Secret, channel.Privacy);
		}

		[Test]
		public void Ctor_FullArg_VibrationPattern_IntCastFromLongArray()
		{
			var pattern = new long[] { 100L, 200L, 300L };

			var channel = new GameNotificationChannel(
				"id",
				"name",
				"description",
				GameNotificationChannel.NotificationStyle.Default,
				vibrationPattern: pattern);

			Assert.IsNotNull(channel.VibrationPattern);
			Assert.AreEqual(3, channel.VibrationPattern.Length);
			Assert.AreEqual(100, channel.VibrationPattern[0]);
			Assert.AreEqual(200, channel.VibrationPattern[1]);
			Assert.AreEqual(300, channel.VibrationPattern[2]);
		}

		[Test]
		public void Ctor_FullArg_NullVibrationPattern_PropertyIsNull()
		{
			var channel = new GameNotificationChannel(
				"id",
				"name",
				"description",
				GameNotificationChannel.NotificationStyle.Default,
				vibrationPattern: null);

			Assert.IsNull(channel.VibrationPattern);
		}
	}
}
