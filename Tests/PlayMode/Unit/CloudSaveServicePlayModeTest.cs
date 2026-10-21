using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameLovers.MobileServices.GamePlatform;
using GameLovers.MobileServices.GamePlatform.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class CloudSaveServicePlayModeTest
	{
		private sealed class FakeCloudBackend : ICloudSaveBackend
		{
			public readonly List<string> Calls = new List<string>();
			public IReadOnlyList<CloudSaveVersion> Versions = new CloudSaveVersion[0];
			public bool Available = true;
			public CloudSaveVersion Resolved;

			public bool IsAvailable => Available;

			public Awaitable<IReadOnlyList<CloudSaveSlot>> ListAsync()
			{
				Calls.Add("list");
				return Completed<IReadOnlyList<CloudSaveSlot>>(new[] { new CloudSaveSlot("slot1", "d", DateTime.UtcNow, "device") });
			}

			public Awaitable<IReadOnlyList<CloudSaveVersion>> LoadVersionsAsync(string slot)
			{
				Calls.Add($"load:{slot}");
				return Completed(Versions);
			}

			public Awaitable ResolveAsync(string slot, CloudSaveVersion chosen)
			{
				Calls.Add($"resolve:{slot}");
				Resolved = chosen;
				return Completed();
			}

			public Awaitable SaveAsync(string slot, byte[] data, string description)
			{
				Calls.Add($"save:{slot}:{data.Length}:{description}");
				return Completed();
			}

			public Awaitable DeleteAsync(string slot)
			{
				Calls.Add($"delete:{slot}");
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

		private FakeCloudBackend _backend;
		private bool _authenticated;
		private CloudSaveService _service;

		[SetUp]
		public void Init()
		{
			_backend = new FakeCloudBackend();
			_authenticated = true;
			_service = new CloudSaveService(() => _authenticated, _backend);
		}

		private static async Awaitable<Exception> Catch<T>(Func<Awaitable<T>> action)
		{
			try
			{
				await action();
				return null;
			}
			catch (Exception exception)
			{
				return exception;
			}
		}

		[UnityTest]
		// ADMIT: CloudSaveService could reach the platform without a signed-in player, where the platform call fails opaquely.
		// RCR: CloudSaveService.cs ThrowIfUnavailable — signed-in guard disabled → RED (An operation did not throw a CloudSaveException. Expected: not null But was: null). 2026-10-21
		public IEnumerator Operations_WhenSignedOut_ThrowNotAuthenticatedWithoutCallingThePlatform()
		{
			_authenticated = false;

			yield return AssertFailsWith(CloudSaveFailure.NotAuthenticated);
		}

		[UnityTest]
		// ADMIT: CloudSaveService could attempt operations while the platform's cloud storage is off.
		// RCR: CloudSaveService.cs ThrowIfUnavailable — storage guard disabled → RED (An operation did not throw a CloudSaveException. Expected: not null But was: null). 2026-10-21
		public IEnumerator Operations_WhenCloudUnavailable_ThrowCloudUnavailableWithoutCallingThePlatform()
		{
			_backend.Available = false;

			yield return AssertFailsWith(CloudSaveFailure.CloudUnavailable);
		}

		[Test]
		// ADMIT: CloudSaveService.IsAvailable could ignore either the sign-in or the storage state.
		// RCR: CloudSaveService.cs IsAvailable — storage state ignored → RED (Expected: False But was: True). 2026-10-21
		public void IsAvailable_RequiresSignInAndStorage()
		{
			Assert.IsTrue(_service.IsAvailable);

			_backend.Available = false;
			Assert.IsFalse(_service.IsAvailable);

			_backend.Available = true;
			_authenticated = false;
			Assert.IsFalse(_service.IsAvailable);
		}

		[UnityTest]
		// ADMIT: CloudSaveService could pass an invalid slot name to the platform, which rejects or mangles it differently per platform.
		// RCR: CloudSaveService.cs ValidateSlot — name check disabled → RED (Expected: instance of <System.ArgumentException> But was: null). 2026-10-21
		public IEnumerator InvalidSlotNames_AreRejectedBeforeTheyReachThePlatform()
		{
			var bad = new[] { string.Empty, "has space", "a/b", new string('x', 101) };
			var results = bad.Select(name => Catch(() => _service.LoadAsync(name))).ToList();
			var nullName = Catch(() => _service.LoadAsync(null));
			foreach (var result in results) yield return result;
			yield return nullName;

			foreach (var result in results) Assert.IsInstanceOf<ArgumentException>(result.GetAwaiter().GetResult());
			Assert.IsInstanceOf<ArgumentNullException>(nullName.GetAwaiter().GetResult());
			Assert.IsEmpty(_backend.Calls);
		}

		[UnityTest]
		// ADMIT: CloudSaveService.SaveAsync could accept a null or oversized save that the platforms reject or truncate.
		// RCR: CloudSaveService.cs SaveAsync — size limit disabled → RED (Expected: instance of <System.ArgumentException> But was: null). 2026-10-21
		public IEnumerator SaveAsync_InvalidData_IsRejectedBeforeItReachesThePlatform()
		{
			var missing = Catch(() => _service.SaveAsync("slot1", null));
			var oversized = Catch(() => _service.SaveAsync("slot1", new byte[CloudSaveService.MaxDataBytes + 1]));
			yield return missing;
			yield return oversized;

			Assert.IsInstanceOf<ArgumentNullException>(missing.GetAwaiter().GetResult());
			Assert.IsInstanceOf<ArgumentException>(oversized.GetAwaiter().GetResult());
			Assert.IsEmpty(_backend.Calls);
		}

		[UnityTest]
		// ADMIT: CloudSaveService could drop the slot, size or description on the way to the platform.
		// RCR: CloudSaveService.cs SaveAsync — description dropped → RED (Expected is <System.String[3]>, actual is <System.Collections.Generic.List`1[System.Str...). 2026-10-21
		public IEnumerator Save_List_Delete_ForwardTheirArguments()
		{
			var save = _service.SaveAsync("slot1", new byte[] { 1, 2, 3 }, "level 4");
			yield return save;
			var delete = _service.DeleteAsync("slot2");
			yield return delete;
			var list = _service.ListAsync();
			yield return list;

			CollectionAssert.AreEqual(new[] { "save:slot1:3:level 4", "delete:slot2", "list" }, _backend.Calls);
			Assert.AreEqual("slot1", list.GetAwaiter().GetResult()[0].Name);
		}

		[UnityTest]
		// ADMIT: CloudSaveService.LoadAsync could return an empty array instead of null for a slot that was never saved.
		// RCR: CloudSaveService.cs LoadAsync — missing slot returns an empty array → RED (Expected: null But was: <empty>). 2026-10-21
		public IEnumerator LoadAsync_MissingSlot_ReturnsNull()
		{
			var operation = _service.LoadAsync("slot1");
			yield return operation;

			Assert.IsNull(operation.GetAwaiter().GetResult());
		}

		[UnityTest]
		// ADMIT: CloudSaveService.LoadAsync could run conflict resolution for a slot that has a single version.
		// RCR: CloudSaveService.cs LoadAsync — single-version shortcut disabled → RED (Expected: not collection containing "resolve:slot1" But was: < "load:slot1", "resolve:s...). 2026-10-21
		public IEnumerator LoadAsync_SingleVersion_ReturnsItWithoutResolving()
		{
			_backend.Versions = new[] { Version("only", 1) };

			var operation = _service.LoadAsync("slot1");
			yield return operation;

			Assert.AreEqual("only", System.Text.Encoding.UTF8.GetString(operation.GetAwaiter().GetResult()));
			CollectionAssert.DoesNotContain(_backend.Calls, "resolve:slot1");
		}

		[UnityTest]
		// ADMIT: CloudSaveService.LoadAsync could keep the older of two conflicting versions under the MostRecent policy.
		// RCR: CloudSaveService.cs Choose — sorted oldest first → RED (Expected string length 3 but was 5. Strings differ at index 0. Expected: "new" But was:...). 2026-10-21
		public IEnumerator LoadAsync_ConflictWithMostRecentPolicy_KeepsTheNewestVersion()
		{
			_backend.Versions = new[] { Version("old", 30), Version("new", 1), Version("older", 90) };

			var operation = _service.LoadAsync("slot1");
			yield return operation;

			Assert.AreEqual("new", System.Text.Encoding.UTF8.GetString(operation.GetAwaiter().GetResult()));
			Assert.AreEqual("new", System.Text.Encoding.UTF8.GetString(_backend.Resolved.Data));
		}

		[UnityTest]
		// ADMIT: CloudSaveService.LoadAsync could skip the resolver under the Manual policy or hand it the versions oldest first.
		// RCR: CloudSaveService.cs Choose — Manual policy treated as MostRecent → RED (Expected string length 6 but was 3. Strings differ at index 0. Expected: "merged" But w...). 2026-10-21
		public IEnumerator LoadAsync_ConflictWithManualPolicy_UsesTheResolversChoice()
		{
			_service.ConflictPolicy = CloudSaveConflictPolicy.Manual;
			_backend.Versions = new[] { Version("old", 30), Version("new", 1) };
			CloudSaveConflict seen = null;
			_service.ConflictResolver = conflict =>
			{
				seen = conflict;
				return Version("merged", 0);
			};

			var operation = _service.LoadAsync("slot1");
			yield return operation;

			Assert.AreEqual("merged", System.Text.Encoding.UTF8.GetString(operation.GetAwaiter().GetResult()));
			Assert.AreEqual("merged", System.Text.Encoding.UTF8.GetString(_backend.Resolved.Data));
			Assert.AreEqual("slot1", seen.SlotName);
			Assert.AreEqual("new", System.Text.Encoding.UTF8.GetString(seen.Versions[0].Data));
		}

		[UnityTest]
		// ADMIT: CloudSaveService.LoadAsync could silently pick a version under the Manual policy when no resolver is set.
		// RCR: CloudSaveService.cs Choose — Conflict reason replaced by Io → RED (Expected: Conflict But was: Io). 2026-10-21
		public IEnumerator LoadAsync_ManualPolicyWithoutResolver_ThrowsConflict()
		{
			_service.ConflictPolicy = CloudSaveConflictPolicy.Manual;
			_backend.Versions = new[] { Version("a", 5), Version("b", 1) };

			var operation = Catch(() => _service.LoadAsync("slot1"));
			yield return operation;

			var exception = operation.GetAwaiter().GetResult() as CloudSaveException;
			Assert.IsNotNull(exception);
			Assert.AreEqual(CloudSaveFailure.Conflict, exception.Reason);
			CollectionAssert.DoesNotContain(_backend.Calls, "resolve:slot1");
		}

		private static async Awaitable<Exception> Catch(Func<Awaitable> action)
		{
			try
			{
				await action();
				return null;
			}
			catch (Exception exception)
			{
				return exception;
			}
		}

		private static CloudSaveVersion Version(string text, int minutesAgo) =>
			new CloudSaveVersion(System.Text.Encoding.UTF8.GetBytes(text), string.Empty, DateTime.UtcNow.AddMinutes(-minutesAgo), "device");

		private IEnumerator AssertFailsWith(CloudSaveFailure reason)
		{
			var results = new[]
			{
				Catch(() => _service.ListAsync()),
				Catch(() => _service.LoadAsync("slot1")),
				Catch(() => _service.SaveAsync("slot1", new byte[] { 1 })),
				Catch(() => _service.DeleteAsync("slot1")),
			};
			foreach (var result in results) yield return result;

			foreach (var result in results)
			{
				var exception = result.GetAwaiter().GetResult() as CloudSaveException;
				Assert.IsNotNull(exception, "An operation did not throw a CloudSaveException.");
				Assert.AreEqual(reason, exception.Reason);
			}
			Assert.IsEmpty(_backend.Calls);
		}
	}
}
