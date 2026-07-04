using System;

// ReSharper disable once CheckNamespace

namespace GameLovers.MobileServices.Notifications
{
	/// <summary>
	/// Notification to serialize/deserialize to disk when the game goes foreground
	/// </summary>
	[Serializable]
	internal struct SerializableNotification
	{
		public bool HasId;
		public int Id;
		public string Title;
		public string Body;
		public string Subtitle;
		public string Channel;
		public bool HasBadgeNumber;
		public int BadgeNumber;
		public bool HasDeliveryTime;
		public long DeliveryTimeBinary;

		internal DateTime? GetDeliveryTime() =>
			HasDeliveryTime ? DateTime.FromBinary(DeliveryTimeBinary) : (DateTime?)null;
	}

	/// <summary>
	/// Converter serialization classes
	/// </summary>
	internal static class SerializableNotificationConverter
	{
		public static IGameNotification AsGameNotification(this SerializableNotification serializableNotification,
			IGameNotificationsPlatform platform)
		{
			var notification = platform.CreateNotification();

			notification.Id = ReadNullableInt(serializableNotification.HasId, serializableNotification.Id);
			notification.Title = serializableNotification.Title;
			notification.Body = serializableNotification.Body;
			notification.Subtitle = serializableNotification.Subtitle;
			notification.Channel = serializableNotification.Channel;
			notification.BadgeNumber = ReadNullableInt(
				serializableNotification.HasBadgeNumber,
				serializableNotification.BadgeNumber);
			notification.DeliveryTime = ReadNullableDateTime(
				serializableNotification.HasDeliveryTime,
				serializableNotification.DeliveryTimeBinary);

			return notification;
		}

		public static SerializableNotification AsSerializableNotification(this PendingNotification pendingNotification)
		{
			var source = pendingNotification.Notification;
			var deliveryTime = source.DeliveryTime;

			return new SerializableNotification
			{
				HasId = source.Id.HasValue,
				Id = source.Id ?? 0,
				Title = source.Title,
				Body = source.Body,
				Subtitle = source.Subtitle,
				Channel = source.Channel,
				HasBadgeNumber = source.BadgeNumber.HasValue,
				BadgeNumber = source.BadgeNumber ?? 0,
				HasDeliveryTime = deliveryTime.HasValue,
				DeliveryTimeBinary = deliveryTime?.ToBinary() ?? 0L,
			};
		}

		private static int? ReadNullableInt(bool hasValue, int value) => hasValue ? value : (int?)null;

		private static DateTime? ReadNullableDateTime(bool hasValue, long binary) =>
			hasValue ? DateTime.FromBinary(binary) : (DateTime?)null;
	}
}
