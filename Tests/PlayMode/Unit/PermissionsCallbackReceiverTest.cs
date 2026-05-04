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
		public IEnumerator OnPermissionResult_UnknownId_NoOp()
		{
			var receiver = PermissionsCallbackReceiver.Instance;

			receiver.OnPermissionResult($"99999:{(int) PermissionStatus.Denied}");
			yield return null;

			Assert.Pass();
		}
	}
}
