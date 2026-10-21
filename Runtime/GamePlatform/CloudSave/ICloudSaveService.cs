using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform
{
	/// <summary>
	/// Why a cloud save operation failed.
	/// </summary>
	public enum CloudSaveFailure
	{
		NotAuthenticated, // No player is signed in to the platform's game service.
		CloudUnavailable, // The platform's cloud storage is off (iCloud Drive disabled, Play Games saved games unavailable).
		Conflict, // Versions of a slot conflict and the Manual policy has no resolver.
		Io // The platform failed to read or write the save.
	}

	/// <summary>
	/// How a cloud save conflict between devices is decided.
	/// </summary>
	public enum CloudSaveConflictPolicy
	{
		MostRecent, // Keep the most recently modified version.
		Manual // Ask the conflict resolver.
	}

	/// <summary>
	/// Thrown by <see cref="ICloudSaveService"/> when the platform cannot complete an operation.
	/// </summary>
	public sealed class CloudSaveException : Exception
	{
		/// <summary>The category of failure.</summary>
		public CloudSaveFailure Reason { get; }

		public CloudSaveException(CloudSaveFailure reason, string message, Exception inner = null) : base(message, inner)
		{
			Reason = reason;
		}
	}

	/// <summary>
	/// One cloud save slot as listed by <see cref="ICloudSaveService.ListAsync"/>.
	/// </summary>
	public sealed class CloudSaveSlot
	{
		/// <summary>The slot name.</summary>
		public string Name { get; }

		/// <summary>The description saved with it. Game Center has none, so it is empty on iOS.</summary>
		public string Description { get; }

		/// <summary>When it was last saved, in UTC.</summary>
		public DateTime ModifiedUtc { get; }

		/// <summary>The device that saved it, when the platform reports one.</summary>
		public string DeviceName { get; }

		public CloudSaveSlot(string name, string description, DateTime modifiedUtc, string deviceName)
		{
			Name = name;
			Description = description;
			ModifiedUtc = modifiedUtc;
			DeviceName = deviceName;
		}
	}

	/// <summary>
	/// One stored version of a slot: its bytes and who saved it.
	/// </summary>
	public sealed class CloudSaveVersion
	{
		/// <summary>The saved bytes.</summary>
		public byte[] Data { get; }

		/// <summary>The description saved with it, or empty.</summary>
		public string Description { get; }

		/// <summary>When it was saved, in UTC.</summary>
		public DateTime ModifiedUtc { get; }

		/// <summary>The device that saved it, when known.</summary>
		public string DeviceName { get; }

		public CloudSaveVersion(byte[] data, string description, DateTime modifiedUtc, string deviceName)
		{
			Data = data;
			Description = description;
			ModifiedUtc = modifiedUtc;
			DeviceName = deviceName;
		}
	}

	/// <summary>
	/// Two or more versions of one slot that devices saved independently.
	/// </summary>
	public sealed class CloudSaveConflict
	{
		/// <summary>The slot in conflict.</summary>
		public string SlotName { get; }

		/// <summary>Every conflicting version, most recently modified first. Always at least two.</summary>
		public IReadOnlyList<CloudSaveVersion> Versions { get; }

		public CloudSaveConflict(string slotName, IReadOnlyList<CloudSaveVersion> versions)
		{
			SlotName = slotName;
			Versions = versions;
		}
	}

	/// <summary>
	/// Slot-based cloud saves on the platform's own storage: Game Center saved games (iCloud) on iOS and Google Play
	/// Games saved games on Android. Reached through <see cref="IGamePlatformServices.CloudSave"/>.
	/// </summary>
	/// <remarks>
	/// Every operation needs a signed-in player and available cloud storage, and otherwise throws a
	/// <see cref="CloudSaveException"/>. Slot names use letters, digits, <c>-</c>, <c>.</c>, <c>_</c> and <c>~</c>, 1 to 100
	/// characters, and a save is at most <see cref="CloudSaveService.MaxDataBytes"/> bytes.
	/// </remarks>
	public interface ICloudSaveService
	{
		/// <summary>True when a player is signed in and the platform's cloud storage is available.</summary>
		bool IsAvailable { get; }

		/// <summary>
		/// How a conflict between devices is decided. Defaults to <see cref="CloudSaveConflictPolicy.MostRecent"/>.
		/// </summary>
		CloudSaveConflictPolicy ConflictPolicy { get; set; }

		/// <summary>
		/// Picks the version to keep when <see cref="ConflictPolicy"/> is <see cref="CloudSaveConflictPolicy.Manual"/>.
		/// Return one of the conflict's versions or a new one built from merged data.
		/// </summary>
		Func<CloudSaveConflict, CloudSaveVersion> ConflictResolver { get; set; }

		/// <summary>
		/// Lists the saved slots.
		/// </summary>
		Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync();

		/// <summary>
		/// Reads a slot, resolving a conflict by <see cref="ConflictPolicy"/> first.
		/// </summary>
		/// <param name="slot">The slot name.</param>
		/// <returns>The saved bytes, or null when the slot does not exist.</returns>
		Awaitable<byte[]> LoadAsync(string slot);

		/// <summary>
		/// Writes a slot, replacing its previous content. On iOS the write continues if the app is backgrounded.
		/// </summary>
		/// <param name="slot">The slot name.</param>
		/// <param name="data">The bytes to save.</param>
		/// <param name="description">An optional description; Game Center ignores it.</param>
		Awaitable SaveAsync(string slot, byte[] data, string description = null);

		/// <summary>
		/// Deletes a slot. Deleting a slot that does not exist does nothing.
		/// </summary>
		Awaitable DeleteAsync(string slot);
	}
}
