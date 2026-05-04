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
		public void SubmitPoint_SamePosition_SkipsAccumulation()
		{
			var gesture = new ActiveGesture(0, Vector2.zero, 0.0);

			gesture.SubmitPoint(Vector2.zero, 0.5);

			Assert.AreEqual(1, gesture.Samples);
			Assert.AreEqual(0f, gesture.TravelDistance);
			Assert.AreEqual(0.5, gesture.EndTime);
		}

		[Test]
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
