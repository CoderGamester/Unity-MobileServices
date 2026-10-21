using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>One platform's saved-game storage; failures are reported as <see cref="CloudSaveException"/>.</summary>
	internal interface ICloudSaveBackend
	{
		/// <summary>True when the platform's storage can be used right now.</summary>
		bool IsAvailable { get; }

		/// <summary>Lists the saved slots.</summary>
		Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync();

		/// <summary>Reads every stored version of a slot, newest first; several mean a conflict.</summary>
		Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot);

		/// <summary>Collapses a conflicting slot to the chosen version.</summary>
		Awaitable ResolveAsync(string slot, CloudSaveVersion chosen);

		/// <summary>Writes a slot, replacing its content.</summary>
		Awaitable SaveAsync(string slot, byte[] data, string description);

		/// <summary>Deletes a slot; a missing slot is not an error.</summary>
		Awaitable DeleteAsync(string slot);
	}
}
