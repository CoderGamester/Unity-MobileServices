using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>One platform's saved-game storage: Game Center, Play Games snapshots, the Editor simulation or none.</summary>
	/// <remarks>Failures are reported as <see cref="CloudSaveException"/>.</remarks>
	internal interface ICloudSaveBackend
	{
		bool IsAvailable { get; }

		Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync();

		/// <summary>Every stored version of a slot, newest first: none when missing, one when clean, several when in conflict.</summary>
		Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot);

		/// <summary>Collapses a conflicting slot to <paramref name="chosen"/>.</summary>
		Awaitable ResolveAsync(string slot, CloudSaveVersion chosen);

		Awaitable SaveAsync(string slot, byte[] data, string description);

		Awaitable DeleteAsync(string slot);
	}
}
