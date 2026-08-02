using GameLovers.MobileServices.Notifications;
using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class GameNotificationChannelTest
	{
		[Test]
		// ADMIT: GameNotificationChannel's 3-arg ctor could pick a different default NotificationStyle, silently downgrading Android importance.
		// RCR: GameNotificationChannel.cs GameNotificationChannel(string,string,string) — `Style = NotificationStyle.Popup` → `Default` → RED (expected Popup was Default).
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
		// ADMIT: GameNotificationChannel's full ctor could drop a caller-supplied flag on the floor.
		// RCR: GameNotificationChannel.cs GameNotificationChannel(10-arg) — `HighPriority = highPriority` → `= false` → RED (expected True was False).
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
		// ADMIT: GameNotificationChannel's long[]→int[] vibration-pattern projection could corrupt the timings it forwards to Android.
		// RCR: GameNotificationChannel.cs GameNotificationChannel(10-arg) — `Select(v => (int)v)` → `(int)v + 1` → RED (VibrationPattern[0] expected 100 was 101).
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
		// ADMIT: GameNotificationChannel could substitute an empty array for a null vibration pattern, overriding the OS default pattern.
		// RCR: GameNotificationChannel.cs GameNotificationChannel(10-arg) — else-branch `VibrationPattern = null` → `new int[0]` → RED (expected null was int[0]).
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
