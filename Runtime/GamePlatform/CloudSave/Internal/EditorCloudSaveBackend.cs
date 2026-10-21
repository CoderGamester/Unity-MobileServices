#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>
	/// Process-wide Editor simulation of cloud saves, read by every <see cref="CloudSaveService"/> in the Editor and
	/// driven by the Device Simulator panel. Saves live under <c>Library/GameLovers/SimulatedCloudSave</c>.
	/// </summary>
	internal static class EditorCloudSaveState
	{
		private const string ConflictSuffix = ".conflict";

		/// <summary>Whether the simulated cloud storage is available, to exercise the unavailable path.</summary>
		public static bool Available = true;

		/// <summary>The folder that holds the simulated saves.</summary>
		public static string Folder =>
			Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "GameLovers", "SimulatedCloudSave");

		/// <summary>Lists the simulated slots.</summary>
		public static IReadOnlyList<CloudSaveSlot> ListSlots()
		{
			var slots = new List<CloudSaveSlot>();
			if (!Directory.Exists(Folder)) return slots;
			foreach (var file in Directory.GetFiles(Folder, "*.json"))
			{
				var name = Path.GetFileNameWithoutExtension(file);
				if (name.EndsWith(ConflictSuffix, StringComparison.Ordinal)) continue;
				var stored = ReadMeta(name);
				slots.Add(new CloudSaveSlot(name, stored.description, new DateTime(stored.modifiedTicksUtc, DateTimeKind.Utc), stored.device));
			}
			return slots;
		}

		/// <summary>Reads every stored version of a slot, newest first.</summary>
		public static IReadOnlyList<CloudSaveVersion> ReadVersions(string slot)
		{
			var versions = new List<CloudSaveVersion>();
			AddVersion(versions, slot);
			AddVersion(versions, slot + ConflictSuffix);
			versions.Sort((left, right) => right.ModifiedUtc.CompareTo(left.ModifiedUtc));
			return versions;
		}

		/// <summary>Stores bytes for a slot, or as its conflicting version.</summary>
		public static void Write(string slot, byte[] data, string description, DateTime modifiedUtc, bool asConflict = false)
		{
			var key = asConflict ? slot + ConflictSuffix : slot;
			Directory.CreateDirectory(Folder);
			File.WriteAllBytes(Path.Combine(Folder, key + ".bin"), data);
			var stored = new StoredSlot { description = description ?? string.Empty, modifiedTicksUtc = modifiedUtc.Ticks, device = asConflict ? "Other device (simulated)" : "Simulator" };
			File.WriteAllText(Path.Combine(Folder, key + ".json"), JsonUtility.ToJson(stored));
		}

		/// <summary>Deletes a slot and its conflicting version.</summary>
		public static void Delete(string slot)
		{
			DeleteFiles(slot);
			DeleteFiles(slot + ConflictSuffix);
		}

		/// <summary>Deletes every simulated save.</summary>
		public static void DeleteAll()
		{
			if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
		}

		/// <summary>Adds a second, newer version of an existing slot, as if another device had saved it.</summary>
		public static bool InjectConflict(string slot, byte[] data)
		{
			if (!File.Exists(Path.Combine(Folder, slot + ".json"))) return false;
			Write(slot, data, "Injected conflict", DateTime.UtcNow, asConflict: true);
			return true;
		}

		private static void AddVersion(List<CloudSaveVersion> versions, string key)
		{
			var binary = Path.Combine(Folder, key + ".bin");
			if (!File.Exists(binary)) return;
			var stored = ReadMeta(key);
			versions.Add(new CloudSaveVersion(File.ReadAllBytes(binary), stored.description, new DateTime(stored.modifiedTicksUtc, DateTimeKind.Utc), stored.device));
		}

		private static StoredSlot ReadMeta(string key)
		{
			var file = Path.Combine(Folder, key + ".json");
			return File.Exists(file) ? JsonUtility.FromJson<StoredSlot>(File.ReadAllText(file)) : new StoredSlot { description = string.Empty, device = "Simulator" };
		}

		private static void DeleteFiles(string key)
		{
			foreach (var extension in new[] { ".bin", ".json" })
			{
				var file = Path.Combine(Folder, key + extension);
				if (File.Exists(file)) File.Delete(file);
			}
		}

		[Serializable]
		private sealed class StoredSlot
		{
			public string description;
			public long modifiedTicksUtc;
			public string device;
		}
	}

	/// <summary>Editor backend that stores saves on disk instead of calling a platform.</summary>
	internal sealed class EditorCloudSaveBackend : ICloudSaveBackend
	{
		/// <inheritdoc />
		public bool IsAvailable => EditorCloudSaveState.Available;

		/// <inheritdoc />
		public Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync() => Completed(EditorCloudSaveState.ListSlots());

		/// <inheritdoc />
		public Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot) => Completed(EditorCloudSaveState.ReadVersions(slot));

		/// <inheritdoc />
		public Awaitable ResolveAsync(string slot, CloudSaveVersion chosen)
		{
			EditorCloudSaveState.Delete(slot);
			EditorCloudSaveState.Write(slot, chosen.Data, chosen.Description, DateTime.UtcNow);
			return Completed();
		}

		/// <inheritdoc />
		public Awaitable SaveAsync(string slot, byte[] data, string description)
		{
			EditorCloudSaveState.Delete(slot);
			EditorCloudSaveState.Write(slot, data, description, DateTime.UtcNow);
			return Completed();
		}

		/// <inheritdoc />
		public Awaitable DeleteAsync(string slot)
		{
			EditorCloudSaveState.Delete(slot);
			return Completed();
		}

		private static Awaitable<T> Completed<T>(T result)
		{
			var completion = new AwaitableCompletionSource<T>();
			completion.SetResult(result);
			return completion.Awaitable;
		}

		private static Awaitable Completed()
		{
			var completion = new AwaitableCompletionSource();
			completion.SetResult();
			return completion.Awaitable;
		}
	}
}
#endif
