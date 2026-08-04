using System.Collections;
using System.Threading.Tasks;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Device.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class PermissionsCallbackReceiverTest
	{
		[TearDown]
		public void Cleanup()
		{
			var go = GameObject.Find("PermissionsCallbackReceiver");
			if (go != null)
			{
				Object.Destroy(go);
			}
		}

		[UnityTest]
		// ADMIT: PermissionsCallbackReceiver.OnPermissionResult could slice the status off the '<id>:<status>' bridge payload and never resolve the pending TCS.
		// RCR: PermissionsCallbackReceiver.cs OnPermissionResult — `payload.Substring(sep + 1)` → `sep + 2` → RED (tcs.Task.IsCompleted expected True was False).
		public IEnumerator OnPermissionResult_ValidPayload_ResolvesPendingTcs()
		{
			var receiver = PermissionsCallbackReceiver.Instance;
			var tcs = new TaskCompletionSource<PermissionStatus>();
			var id = receiver.Register(tcs);

			receiver.OnPermissionResult($"{id}:{(int) PermissionStatus.Granted}");

			yield return null;

			Assert.IsTrue(tcs.Task.IsCompleted);
			Assert.AreEqual(PermissionStatus.Granted, tcs.Task.Result);
		}

		[UnityTest]
		// ADMIT: PermissionsCallbackReceiver.OnPermissionResult could resolve a TCS from an unparseable status, handing callers a bogus PermissionStatus.
		// RCR: PermissionsCallbackReceiver.cs OnPermissionResult — int.TryParse guard `||` → `&&` → RED ('<id>:not-an-int' resolves the TCS; expected IsCompleted False).
		public IEnumerator OnPermissionResult_MalformedPayload_DoesNotThrow()
		{
			var receiver = PermissionsCallbackReceiver.Instance;
			var tcs = new TaskCompletionSource<PermissionStatus>();
			var id = receiver.Register(tcs);

			Assert.DoesNotThrow(() => receiver.OnPermissionResult("not-a-valid-payload"));
			Assert.DoesNotThrow(() => receiver.OnPermissionResult($"{id}:not-an-int"));
			Assert.DoesNotThrow(() => receiver.OnPermissionResult(":1"));

			yield return null;

			Assert.IsFalse(tcs.Task.IsCompleted, "Malformed payloads should not resolve the registered TCS.");
		}

		[UnityTest]
		// ADMIT: PermissionsCallbackReceiver.OnPermissionResult must key the pending lookup on the payload's
		// id, or a stray native callback resolves an unrelated caller's TaskCompletionSource.
		// RCR: PermissionsCallbackReceiver.cs OnPermissionResult - `_pending.TryGetValue(id, out var tcs)` ->
		// `_pending.TryGetValue(_nextId - 1, out var tcs)` -> RED (tcs.Task.IsCompleted expected False, was
		// True). The sibling valid-payload test stays green because there id == _nextId - 1. 2026-08-04
		public IEnumerator OnPermissionResult_UnknownId_NoOp()
		{
			var receiver = PermissionsCallbackReceiver.Instance;
			var tcs = new TaskCompletionSource<PermissionStatus>();
			var id = receiver.Register(tcs);

			receiver.OnPermissionResult($"{id + 99999}:{(int) PermissionStatus.Denied}");
			yield return null;

			Assert.IsFalse(tcs.Task.IsCompleted,
				"A result addressed to an unregistered id must not resolve somebody else's pending request.");
			LogAssert.NoUnexpectedReceived();
		}
	}
}
