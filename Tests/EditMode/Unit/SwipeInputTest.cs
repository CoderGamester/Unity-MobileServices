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
		// ADMIT: SwipeInput's ctor could compute SwipeDirection backwards, reporting every swipe as its opposite.
		// RCR: SwipeInput.cs SwipeInput(ActiveGesture) — `(EndPosition - StartPosition).normalized` → `(StartPosition - EndPosition)` → RED (expected (1,0) was (-1,0)).
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
		// ADMIT: SwipeInput's ctor could divide by a zero SwipeDuration and produce a NaN velocity.
		// RCR: SwipeInput.cs SwipeInput(ActiveGesture) — `if (SwipeDuration > 0.0f)` → `>= 0.0f` → RED (SwipeVelocity expected 0 was NaN).
		public void Ctor_FromGesture_ZeroDuration_VelocityRemainsZero()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 1.0);
			// No SubmitPoint → EndTime == StartTime → SwipeDuration == 0
			var swipe = new SwipeInput(gesture);

			Assert.AreEqual(0.0, swipe.SwipeDuration);
			Assert.AreEqual(0f, swipe.SwipeVelocity);
		}

		[Test]
		// ADMIT: SwipeInput's ctor could compute velocity as travel times duration instead of travel over duration.
		// RCR: SwipeInput.cs SwipeInput(ActiveGesture) — `TravelDistance / SwipeDuration` → `*` → RED (SwipeVelocity expected 400 was 25).
		public void Ctor_FromGesture_PositiveDuration_VelocityIsTravelOverDuration()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 0.0);
			gesture.SubmitPoint(new Vector2(100f, 0f), 0.25);

			var swipe = new SwipeInput(gesture);

			Assert.AreEqual(400f, swipe.SwipeVelocity, 1e-3f);
		}
	}
}
