using System.Collections;
using GameLovers.MobileServices.Gestures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	public class GestureControllerSmokeTest
	{
		private GameObject _go;
		private GestureController _controller;

		[SetUp]
		public void Init()
		{
			_go = new GameObject("GestureController");
			_controller = _go.AddComponent<GestureController>();
		}

		[TearDown]
		public void Cleanup()
		{
			if (_go != null)
			{
				Object.Destroy(_go);
			}

			// EnhancedTouchSupport is enabled in OnEnable; ensure we leave the global state clean.
			if (EnhancedTouchSupport.enabled)
			{
				EnhancedTouchSupport.Disable();
			}
		}

		[UnityTest]
		public IEnumerator OnEnable_EnablesEnhancedTouchSupport_AndOnDisableDisables()
		{
			yield return null;

			Assert.IsTrue(EnhancedTouchSupport.enabled,
				"GestureController.OnEnable should enable EnhancedTouchSupport");

			_controller.enabled = false;
			yield return null;

			Assert.IsFalse(EnhancedTouchSupport.enabled,
				"GestureController.OnDisable should disable EnhancedTouchSupport");
		}

		[UnityTest]
		public IEnumerator Ctor_EmitsNoEventsBeforeFingerInteraction()
		{
			var pressed = 0;
			var swiped = 0;
			var tapped = 0;

			_controller.Pressed += _ => pressed++;
			_controller.Swiped  += _ => swiped++;
			_controller.Tapped  += _ => tapped++;

			yield return null;
			yield return null;

			Assert.AreEqual(0, pressed);
			Assert.AreEqual(0, swiped);
			Assert.AreEqual(0, tapped);
		}
	}
}
