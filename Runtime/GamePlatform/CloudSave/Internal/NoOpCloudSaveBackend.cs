using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>Backend for platforms without saved-game storage: the service reports the cloud as unavailable.</summary>
	internal sealed class NoOpCloudSaveBackend : ICloudSaveBackend
	{
		public bool IsAvailable => false;

		public Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync() => throw Unavailable();

		public Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot) => throw Unavailable();

		public Awaitable ResolveAsync(string slot, CloudSaveVersion chosen) => throw Unavailable();

		public Awaitable SaveAsync(string slot, byte[] data, string description) => throw Unavailable();

		public Awaitable DeleteAsync(string slot) => throw Unavailable();

		private static CloudSaveException Unavailable() =>
			new CloudSaveException(CloudSaveFailure.CloudUnavailable, "Cloud saves are only available on iOS and Android.");
	}
}
