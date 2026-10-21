using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>Backend for platforms without saved-game storage: the cloud is reported as unavailable.</summary>
	internal sealed class NoOpCloudSaveBackend : ICloudSaveBackend
	{
		/// <inheritdoc />
		public bool IsAvailable => false;

		/// <inheritdoc />
		public Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync() => throw Unavailable();

		/// <inheritdoc />
		public Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot) => throw Unavailable();

		/// <inheritdoc />
		public Awaitable ResolveAsync(string slot, CloudSaveVersion chosen) => throw Unavailable();

		/// <inheritdoc />
		public Awaitable SaveAsync(string slot, byte[] data, string description) => throw Unavailable();

		/// <inheritdoc />
		public Awaitable DeleteAsync(string slot) => throw Unavailable();

		private static CloudSaveException Unavailable() =>
			new CloudSaveException(CloudSaveFailure.CloudUnavailable, "Cloud saves are only available on iOS and Android.");
	}
}
