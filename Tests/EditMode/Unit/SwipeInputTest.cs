using GameLovers.MobileServices.Gestures;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class SwipeInputTest
	{
		[Test]
		public void Ctor_FromGesture_ComputesDirectionFromStartEnd()
		{
			var gesture = new ActiveGesture(3, Vector2.zero, 0.0);
			gesture.SubmitPoint(new Vector2(50f, 0f), 0.5);

			var swipe = new SwipeInput(gesture);

			Assert.AreEqual(3, swipe.InputId);
			Assert.AreEqual(Vector2.zero, swipe.StartPosition);
			Assert.AreEqual(new Vector2(50f, 0f), swipe.EndPosition);
			Assert.AreEqual(Vector2.right, swipe.SwipeDirection);
			Assert.AreEqual(0.5, swipe.SwipeDuration);
			Assert.AreEqual(50f, swipe.TravelDistance, 1e-4f);
		}

		[Test]
		public void Ctor_FromGesture_ZeroDuration_VelocityRemainsZero()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 1.0);
			// No SubmitPoint → EndTime == StartTime → SwipeDuration == 0
			var swipe = new SwipeInput(gesture);

			Assert.AreEqual(0.0, swipe.SwipeDuration);
			Assert.AreEqual(0f, swipe.SwipeVelocity);
		}

		[Test]
		public void Ctor_FromGesture_PositiveDuration_VelocityIsTravelOverDuration()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 0.0);
			gesture.SubmitPoint(new Vector2(100f, 0f), 0.25);

			var swipe = new SwipeInput(gesture);

			Assert.AreEqual(400f, swipe.SwipeVelocity, 1e-3f);
		}
	}
}
