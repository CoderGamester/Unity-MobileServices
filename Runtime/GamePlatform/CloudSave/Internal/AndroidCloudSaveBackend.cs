#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>
	/// Google Play Games saved-games (snapshots) backend. Like the sign-in backend it uses JNI so the package compiles
	/// without the SDK.
	/// </summary>
	internal sealed class AndroidCloudSaveBackend : ICloudSaveBackend
	{
		private const string SnapshotsClientClass = "com.google.android.gms.games.SnapshotsClient";

		private readonly Dictionary<string, PendingConflict> _conflicts = new Dictionary<string, PendingConflict>();

		public bool IsAvailable => true;

		public Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync()
		{
			var completion = new AwaitableCompletionSource<IReadOnlyList<CloudSaveSlot>>();
			Run(completion, () => ListMetadata(completion, metadata =>
			{
				var slots = new List<CloudSaveSlot>();
				foreach (var item in metadata) slots.Add(ToSlot(item));
				completion.TrySetResult(slots);
			}));
			return NativeCompletion.ToMainThread(completion);
		}

		public Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot)
		{
			var completion = new AwaitableCompletionSource<IReadOnlyList<CloudSaveVersion>>();
			Run(completion, () => ListMetadata(completion, metadata =>
			{
				var match = Find(metadata, slot);
				if (match == null)
				{
					completion.TrySetResult(Array.Empty<CloudSaveVersion>());
					return;
				}

				var client = Client();
				AndroidTaskListeners.Listen(client.Call<AndroidJavaObject>("open", match, ResolutionPolicy("RESOLUTION_POLICY_MANUAL")), task =>
					Guard(completion, () => OnOpened(client, slot, task, completion)));
			}));
			return NativeCompletion.ToMainThread(completion);
		}

		public Awaitable ResolveAsync(string slot, CloudSaveVersion chosen)
		{
			var completion = new AwaitableCompletionSource<bool>();
			Run(completion, () =>
			{
				PendingConflict pending;
				lock (_conflicts)
				{
					if (!_conflicts.TryGetValue(slot, out pending)) throw new CloudSaveException(CloudSaveFailure.Io, $"Slot '{slot}' has no open conflict to resolve.");
					_conflicts.Remove(slot);
				}

				using var contents = pending.Snapshot.Call<AndroidJavaObject>("getSnapshotContents");
				if (!contents.Call<bool>("writeBytes", chosen.Data)) throw new CloudSaveException(CloudSaveFailure.Io, $"Could not write the resolved data for slot '{slot}'.");

				var client = Client();
				AndroidTaskListeners.Listen(client.Call<AndroidJavaObject>("resolveConflict", pending.ConflictId, pending.Snapshot), task =>
					Guard(completion, () =>
					{
						if (!task.Call<bool>("isSuccessful")) throw Failure(task);
						using var result = task.Call<AndroidJavaObject>("getResult");
						if (result.Call<bool>("isConflict")) throw new CloudSaveException(CloudSaveFailure.Conflict, $"Slot '{slot}' conflicted again while resolving.");
						Discard(client, result.Call<AndroidJavaObject>("getData"));
						completion.TrySetResult(true);
					}));
			});
			return Void(NativeCompletion.ToMainThread(completion));
		}

		public Awaitable SaveAsync(string slot, byte[] data, string description)
		{
			var completion = new AwaitableCompletionSource<bool>();
			Run(completion, () =>
			{
				var client = Client();
				AndroidTaskListeners.Listen(client.Call<AndroidJavaObject>("open", slot, true, ResolutionPolicy("RESOLUTION_POLICY_MOST_RECENTLY_MODIFIED")), task =>
					Guard(completion, () =>
					{
						if (!task.Call<bool>("isSuccessful")) throw Failure(task);
						using var opened = task.Call<AndroidJavaObject>("getResult");
						if (opened.Call<bool>("isConflict")) throw new CloudSaveException(CloudSaveFailure.Conflict, $"Slot '{slot}' is in conflict; load it and resolve it before saving.");

						var snapshot = opened.Call<AndroidJavaObject>("getData");
						using var contents = snapshot.Call<AndroidJavaObject>("getSnapshotContents");
						if (!contents.Call<bool>("writeBytes", data)) throw new CloudSaveException(CloudSaveFailure.Io, $"Could not write the data for slot '{slot}'.");

						using var builder = new AndroidJavaObject("com.google.android.gms.games.snapshot.SnapshotMetadataChange$Builder");
						if (!string.IsNullOrEmpty(description)) builder.Call<AndroidJavaObject>("setDescription", description);
						using var change = builder.Call<AndroidJavaObject>("build");
						AndroidTaskListeners.Listen(client.Call<AndroidJavaObject>("commitAndClose", snapshot, change), commitTask =>
							Guard(completion, () =>
							{
								if (!commitTask.Call<bool>("isSuccessful")) throw Failure(commitTask);
								completion.TrySetResult(true);
							}));
					}));
			});
			return Void(NativeCompletion.ToMainThread(completion));
		}

		public Awaitable DeleteAsync(string slot)
		{
			var completion = new AwaitableCompletionSource<bool>();
			Run(completion, () => ListMetadata(completion, metadata =>
			{
				var match = Find(metadata, slot);
				if (match == null)
				{
					completion.TrySetResult(true);
					return;
				}

				AndroidTaskListeners.Listen(Client().Call<AndroidJavaObject>("delete", match), task =>
					Guard(completion, () =>
					{
						if (!task.Call<bool>("isSuccessful")) throw Failure(task);
						completion.TrySetResult(true);
					}));
			}));
			return Void(NativeCompletion.ToMainThread(completion));
		}

		private static AndroidJavaObject Client()
		{
			using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
			using var playGames = new AndroidJavaClass("com.google.android.gms.games.PlayGames");
			return playGames.CallStatic<AndroidJavaObject>("getSnapshotsClient", activity);
		}

		private static int ResolutionPolicy(string name)
		{
			using var client = new AndroidJavaClass(SnapshotsClientClass);
			return client.GetStatic<int>(name);
		}

		private static CloudSaveException Failure(AndroidJavaObject task) =>
			new CloudSaveException(CloudSaveFailure.Io, AndroidTaskListeners.DescribeFailure(task));

		private static async Awaitable Void(Awaitable<bool> operation)
		{
			await operation;
		}

		// Starts an operation; an exception thrown while starting it fails the awaitable instead of escaping.
		private static void Run<T>(AwaitableCompletionSource<T> completion, Action start) => Guard(completion, start);

		private static void Guard<T>(AwaitableCompletionSource<T> completion, Action action)
		{
			try
			{
				action();
			}
			catch (CloudSaveException exception)
			{
				completion.TrySetException(exception);
			}
			catch (Exception exception)
			{
				completion.TrySetException(new CloudSaveException(CloudSaveFailure.Io, exception.Message, exception));
			}
		}

		// Loads the metadata of every slot, frozen so it stays valid after the buffer is released.
		private static void ListMetadata<T>(AwaitableCompletionSource<T> completion, Action<List<AndroidJavaObject>> onLoaded)
		{
			AndroidTaskListeners.Listen(Client().Call<AndroidJavaObject>("load", false), task =>
				Guard(completion, () =>
				{
					if (!task.Call<bool>("isSuccessful")) throw Failure(task);
					using var annotated = task.Call<AndroidJavaObject>("getResult");
					using var buffer = annotated.Call<AndroidJavaObject>("get");
					var metadata = new List<AndroidJavaObject>();
					try
					{
						var count = buffer.Call<int>("getCount");
						for (var i = 0; i < count; i++)
						{
							using var item = buffer.Call<AndroidJavaObject>("get", i);
							metadata.Add(item.Call<AndroidJavaObject>("freeze"));
						}
					}
					finally
					{
						buffer.Call("release");
					}
					onLoaded(metadata);
				}));
		}

		private static AndroidJavaObject Find(List<AndroidJavaObject> metadata, string slot)
		{
			foreach (var item in metadata)
			{
				if (item.Call<string>("getUniqueName") == slot) return item;
			}
			return null;
		}

		private static CloudSaveSlot ToSlot(AndroidJavaObject metadata) =>
			new CloudSaveSlot(metadata.Call<string>("getUniqueName"), metadata.Call<string>("getDescription") ?? string.Empty,
				FromMilliseconds(metadata.Call<long>("getLastModifiedTimestamp")), metadata.Call<string>("getDeviceName") ?? string.Empty);

		private static DateTime FromMilliseconds(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;

		private static CloudSaveVersion ReadVersion(AndroidJavaObject snapshot)
		{
			using var metadata = snapshot.Call<AndroidJavaObject>("getMetadata");
			using var contents = snapshot.Call<AndroidJavaObject>("getSnapshotContents");
			return new CloudSaveVersion(contents.Call<byte[]>("readFully"), metadata.Call<string>("getDescription") ?? string.Empty,
				FromMilliseconds(metadata.Call<long>("getLastModifiedTimestamp")), metadata.Call<string>("getDeviceName") ?? string.Empty);
		}

		private void OnOpened(AndroidJavaObject client, string slot, AndroidJavaObject task, AwaitableCompletionSource<IReadOnlyList<CloudSaveVersion>> completion)
		{
			if (!task.Call<bool>("isSuccessful")) throw Failure(task);
			using var opened = task.Call<AndroidJavaObject>("getResult");
			if (!opened.Call<bool>("isConflict"))
			{
				var snapshot = opened.Call<AndroidJavaObject>("getData");
				var version = ReadVersion(snapshot);
				Discard(client, snapshot);
				completion.TrySetResult(new[] { version });
				return;
			}

			using var conflict = opened.Call<AndroidJavaObject>("getConflict");
			var current = conflict.Call<AndroidJavaObject>("getSnapshot");
			using var other = conflict.Call<AndroidJavaObject>("getConflictingSnapshot");
			var versions = new List<CloudSaveVersion> { ReadVersion(current), ReadVersion(other) };
			versions.Sort((left, right) => right.ModifiedUtc.CompareTo(left.ModifiedUtc));
			lock (_conflicts) _conflicts[slot] = new PendingConflict(conflict.Call<string>("getConflictId"), current);
			completion.TrySetResult(versions);
		}

		private static void Discard(AndroidJavaObject client, AndroidJavaObject snapshot) =>
			client.Call<AndroidJavaObject>("discardAndClose", snapshot).Dispose();

		private sealed class PendingConflict
		{
			public string ConflictId { get; }
			public AndroidJavaObject Snapshot { get; }

			public PendingConflict(string conflictId, AndroidJavaObject snapshot)
			{
				ConflictId = conflictId;
				Snapshot = snapshot;
			}
		}
	}
}
#endif
