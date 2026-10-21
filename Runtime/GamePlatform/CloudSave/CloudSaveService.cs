using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameLovers.MobileServices.GamePlatform.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform
{
	/// <inheritdoc />
	public sealed class CloudSaveService : ICloudSaveService
	{
		/// <summary>The largest save accepted: the Google Play Games snapshot limit, applied on both platforms.</summary>
		public const int MaxDataBytes = 3 * 1024 * 1024;

		private static readonly Regex _slotName = new Regex("^[A-Za-z0-9._~-]{1,100}$", RegexOptions.Compiled);

		private readonly Func<bool> _isAuthenticated;
		private readonly ICloudSaveBackend _backend;

		/// <inheritdoc />
		public bool IsAvailable => _isAuthenticated() && _backend.IsAvailable;

		/// <inheritdoc />
		public CloudSaveConflictPolicy ConflictPolicy { get; set; } = CloudSaveConflictPolicy.MostRecent;

		/// <inheritdoc />
		public Func<CloudSaveConflict, CloudSaveVersion> ConflictResolver { get; set; }

		internal CloudSaveService(Func<bool> isAuthenticated, ICloudSaveBackend backend)
		{
			_isAuthenticated = isAuthenticated;
			_backend = backend;
		}

		/// <inheritdoc />
		public async Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync()
		{
			ThrowIfUnavailable();
			return await _backend.ListAsync();
		}

		/// <inheritdoc />
		public async Awaitable<byte[]> LoadAsync(string slot)
		{
			ValidateSlot(slot);
			ThrowIfUnavailable();

			var versions = await _backend.LoadVersionsAsync(slot);
			if (versions == null || versions.Count == 0)
			{
				return null;
			}

			if (versions.Count == 1)
			{
				return versions[0].Data;
			}

			var chosen = Choose(slot, versions);
			await _backend.ResolveAsync(slot, chosen);
			return chosen.Data;
		}

		/// <inheritdoc />
		public async Awaitable SaveAsync(string slot, byte[] data, string description = null)
		{
			ValidateSlot(slot);
			if (data == null) throw new ArgumentNullException(nameof(data));
			if (data.Length > MaxDataBytes)
			{
				throw new ArgumentException($"A cloud save is at most {MaxDataBytes} bytes; got {data.Length}.", nameof(data));
			}
			ThrowIfUnavailable();

			await _backend.SaveAsync(slot, data, description);
		}

		/// <inheritdoc />
		public async Awaitable DeleteAsync(string slot)
		{
			ValidateSlot(slot);
			ThrowIfUnavailable();

			await _backend.DeleteAsync(slot);
		}

		private static void ValidateSlot(string slot)
		{
			if (slot == null) throw new ArgumentNullException(nameof(slot));
			if (!_slotName.IsMatch(slot))
			{
				throw new ArgumentException($"'{slot}' is not a valid slot name; use 1 to 100 letters, digits, '-', '.', '_' or '~'.", nameof(slot));
			}
		}

		private CloudSaveVersion Choose(string slot, IReadOnlyList<CloudSaveVersion> versions)
		{
			var newestFirst = new List<CloudSaveVersion>(versions);
			newestFirst.Sort((left, right) => right.ModifiedUtc.CompareTo(left.ModifiedUtc));

			if (ConflictPolicy == CloudSaveConflictPolicy.MostRecent)
			{
				return newestFirst[0];
			}

			var resolver = ConflictResolver;
			if (resolver == null)
			{
				throw new CloudSaveException(CloudSaveFailure.Conflict,
					$"Slot '{slot}' has conflicting versions and the Manual policy has no {nameof(ConflictResolver)}.");
			}

			return resolver(new CloudSaveConflict(slot, newestFirst))
				?? throw new CloudSaveException(CloudSaveFailure.Conflict, $"The conflict resolver for slot '{slot}' returned no version.");
		}

		private void ThrowIfUnavailable()
		{
			if (!_isAuthenticated())
			{
				throw new CloudSaveException(CloudSaveFailure.NotAuthenticated, "Cloud saves need a signed-in player; call IGamePlatformServices.AuthenticateAsync first.");
			}

			if (!_backend.IsAvailable)
			{
				throw new CloudSaveException(CloudSaveFailure.CloudUnavailable, "The platform's cloud storage is not available (iCloud Drive off or Play Games saved games unavailable).");
			}
		}
	}
}
