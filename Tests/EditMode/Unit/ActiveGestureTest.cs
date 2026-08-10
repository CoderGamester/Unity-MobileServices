using GameLovers.MobileServices.Gestures;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class ActiveGestureTest
	{
		[Test]
		// ADMIT: ActiveGesture's constructor could leave EndPosition unseeded so a finger-down with no movement reports a (0,0) end point.
		// RCR: ActiveGesture.cs ActiveGesture(int,Vector2,double) — seed EndPosition to Vector2.zero instead of startPosition → RED (EndPosition expected (100,200) was (0,0)).
		public void Ctor_InitializesFieldsToStartPositionTime()
		{
			var start = new Vector2(100f, 200f);
			var gesture = new ActiveGesture(7, start, 1.5);

			Assert.AreEqual(7, gesture.InputId);
			Assert.AreEqual(start, gesture.StartPosition);
			Assert.AreEqual(start, gesture.EndPosition);
			Assert.AreEqual(1.5, gesture.StartTime);
			Assert.AreEqual(1.5, gesture.EndTime);
			Assert.AreEqual(1, gesture.Samples);
			Assert.AreEqual(0f, gesture.TravelDistance);
		}

		[Test]
		// ADMIT: ActiveGesture.SubmitPoint could drop its zero-distance early return and count a duplicate sample.
		// RCR: ActiveGesture.cs SubmitPoint — replace `if (Mathf.Approximately(distanceMoved, 0))` with `if (false)` → RED (Samples expected 1 was 2).
		public void SubmitPoint_SamePosition_SkipsAccumulation()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 0.0);

			gesture.SubmitPoint(Vector2.zero, 0.5);

			Assert.AreEqual(1, gesture.Samples);
			Assert.AreEqual(0f, gesture.TravelDistance);
			Assert.AreEqual(0.5, gesture.EndTime);
		}

		[Test]
		// ADMIT: ActiveGesture.SubmitPoint could overwrite instead of accumulate TravelDistance, under-reporting swipe length.
		// RCR: ActiveGesture.cs SubmitPoint — `TravelDistance += distanceMoved` → `=` → RED (TravelDistance expected 30 was 10).
		public void SubmitPoint_StraightLine_TravelDistanceMatchesEuclidean()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 0.0);

			gesture.SubmitPoint(new Vector2(10f, 0f), 0.1);
			gesture.SubmitPoint(new Vector2(20f, 0f), 0.2);
			gesture.SubmitPoint(new Vector2(30f, 0f), 0.3);

			Assert.AreEqual(30f, gesture.TravelDistance, 1e-4f);
			Assert.AreEqual(new Vector2(30f, 0f), gesture.EndPosition);
		}

		[Test]
		// ADMIT: ActiveGesture.SubmitPoint could divide the accumulated direction by Samples instead of Samples-1, biasing sameness low for straight swipes.
		// RCR: ActiveGesture.cs SubmitPoint — `accumulatedNormalized / (Samples - 1)` → `/ Samples` → RED (sameness ~0.909 not >= 0.99).
		public void SubmitPoint_StraightLine_SwipeDirectionSamenessApproachesOne()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 0.0);

			for (var i = 1; i <= 10; i++)
			{
				gesture.SubmitPoint(new Vector2(i * 5f, 0f), i * 0.05);
			}

			Assert.GreaterOrEqual(gesture.SwipeDirectionSameness, 0.99f);
		}

		[Test]
		// ADMIT: ActiveGesture.SubmitPoint could stop recomputing SwipeDirectionSameness, leaving the ctor's optimistic 1 in place for a reversing gesture.
		// RCR: ActiveGesture.cs SubmitPoint — replace the SwipeDirectionSameness dot-product assignment with `= 1f` → RED (1 is not < 0.5).
		public void SubmitPoint_BackAndForth_SamenessLow()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 0.0);

			gesture.SubmitPoint(new Vector2(10f, 0f), 0.1);
			gesture.SubmitPoint(new Vector2(0f, 0f), 0.2);
			gesture.SubmitPoint(new Vector2(10f, 0f), 0.3);
			gesture.SubmitPoint(new Vector2(0f, 0f), 0.4);

			Assert.Less(gesture.SwipeDirectionSameness, 0.5f);
		}
	}
}
