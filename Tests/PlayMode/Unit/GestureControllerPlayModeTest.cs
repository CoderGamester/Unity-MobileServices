using GameLovers.MobileServices.Gestures;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public sealed class GestureControllerPlayModeTest
	{
		private GameObject _gameObject;
		private GestureController _controller;
		private int _pressedCount;

		[SetUp]
		public void Setup()
		{
			_gameObject = new GameObject(nameof(GestureController));
			_controller = _gameObject.AddComponent<GestureController>();
			_controller.Pressed += _ => _pressedCount++;
		}

		[TearDown]
		public void TearDown()
		{
			if (_controller != null) _controller.enabled = false;
			if (_gameObject != null) Object.DestroyImmediate(_gameObject);
		}

		[Test]
		// ADMIT: A duplicate finger-down can be delivered for a still-tracked input index and must not abort UI interaction.
		// RCR: GestureController.cs OnPressed — `_activeGestures[inputId] = newGesture` -> `_activeGestures.Add(inputId, newGesture)` -> RED (the duplicate input throws ArgumentException). 2026-08-08
		public void OnPressed_DuplicateFingerDown_DoesNotThrowAndReportsBothPresses()
		{
			_controller.OnPressed(0, new Vector2(32f, 48f), 0d);

			Assert.DoesNotThrow(() => _controller.OnPressed(0, new Vector2(64f, 96f), 0.1d));
			Assert.AreEqual(2, _pressedCount);
		}
	}
}
