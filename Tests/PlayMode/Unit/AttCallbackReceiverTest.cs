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
	public class AttCallbackReceiverTest
	{
		[TearDown]
		public void Cleanup()
		{
			var go = GameObject.Find("AttCallbackReceiver");
			if (go != null)
			{
				Object.Destroy(go);
			}
		}

		[UnityTest]
		public IEnumerator OnAttResult_ValidPayload_ResolvesPendingTcs()
		{
			var receiver = AttCallbackReceiver.Instance;
			var tcs = new TaskCompletionSource<AttStatus>();
			var id = receiver.Register(tcs);

			receiver.OnAttResult($"{id}:{(int) AttStatus.Authorized}");

			yield return null;

			Assert.IsTrue(tcs.Task.IsCompleted);
			Assert.AreEqual(AttStatus.Authorized, tcs.Task.Result);
		}

		[UnityTest]
		public IEnumerator OnAttResult_MalformedPayload_DoesNotThrow()
		{
			var receiver = AttCallbackReceiver.Instance;
			var tcs = new TaskCompletionSource<AttStatus>();
			receiver.Register(tcs);

			Assert.DoesNotThrow(() => receiver.OnAttResult("malformed"));
			Assert.DoesNotThrow(() => receiver.OnAttResult("1:not-int"));
			Assert.DoesNotThrow(() => receiver.OnAttResult(":3"));

			yield return null;

			Assert.IsFalse(tcs.Task.IsCompleted);
		}
	}
}
