using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>
	/// Decodes the tagged strings the iOS saved-game bridge returns: <c>K&lt;payload&gt;</c> for success and
	/// <c>E&lt;reason&gt;|&lt;message&gt;</c> for failure, where the payload is JSON with an <c>items</c> array.
	/// </summary>
	internal static class CloudSaveProtocol
	{
		/// <summary>Returns a successful reply's payload, or throws the failure the reply carries.</summary>
		public static string ReadPayload(string reply)
		{
			if (string.IsNullOrEmpty(reply))
			{
				throw new CloudSaveException(CloudSaveFailure.Io, "The platform returned no response.");
			}

			switch (reply[0])
			{
				case 'K':
					return reply.Substring(1);
				case 'E':
				{
					var separator = reply.IndexOf('|');
					var reasonName = separator < 0 ? reply.Substring(1) : reply.Substring(1, separator - 1);
					var message = separator < 0 ? "The platform reported an error." : reply.Substring(separator + 1);
					var reason = Enum.TryParse(reasonName, out CloudSaveFailure parsed) ? parsed : CloudSaveFailure.Io;
					throw new CloudSaveException(reason, message);
				}
				default:
					throw new CloudSaveException(CloudSaveFailure.Io, $"Unrecognized platform response '{reply}'.");
			}
		}

		/// <summary>Decodes the slot list of a successful reply.</summary>
		public static IReadOnlyList<CloudSaveSlot> ParseSlots(string json)
		{
			var slots = new List<CloudSaveSlot>();
			foreach (var item in Parse(json))
			{
				slots.Add(new CloudSaveSlot(item.name, item.description ?? string.Empty, FromUnixSeconds(item.modified), item.device ?? string.Empty));
			}
			return slots;
		}

		/// <summary>Decodes versions and returns them newest first.</summary>
		public static IReadOnlyList<CloudSaveVersion> ParseVersions(string json)
		{
			var versions = new List<CloudSaveVersion>();
			foreach (var item in Parse(json))
			{
				var data = string.IsNullOrEmpty(item.data) ? Array.Empty<byte>() : Convert.FromBase64String(item.data);
				versions.Add(new CloudSaveVersion(data, item.description ?? string.Empty, FromUnixSeconds(item.modified), item.device ?? string.Empty));
			}
			versions.Sort((left, right) => right.ModifiedUtc.CompareTo(left.ModifiedUtc));
			return versions;
		}

		private static NativeEntry[] Parse(string json) =>
			string.IsNullOrEmpty(json) ? Array.Empty<NativeEntry>() : JsonUtility.FromJson<NativeEntries>(json)?.items ?? Array.Empty<NativeEntry>();

		private static DateTime FromUnixSeconds(double seconds) =>
			new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);

		[Serializable]
		private sealed class NativeEntries
		{
			public NativeEntry[] items;
		}

		[Serializable]
		private sealed class NativeEntry
		{
			public string name;
			public string description;
			public double modified;
			public string device;
			public string data;
		}
	}
}
