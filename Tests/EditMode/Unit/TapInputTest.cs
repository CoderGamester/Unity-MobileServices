using GameLovers.MobileServices.Gestures;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class TapInputTest
	{
		[Test]
		// ADMIT: TapInput's ctor could compute TapDuration with the wrong operator, breaking GestureController's max-tap-duration gate.
		// RCR: TapInput.cs TapInput(ActiveGesture) — `EndTime - StartTime` → `EndTime + StartTime` → RED (TapDuration expected 0.1 was 10.1).
		public void Ctor_FromGesture_CapturesPositionsDurationDriftAndTimestamp()
		{
			var start = new Vector2(10f, 20f);
			var gesture = new ActiveGesture(0, start, 5.0);
			gesture.SubmitPoint(new Vector2(12f, 23f), 5.1);

			var tap = new TapInput(gesture);

			Assert.AreEqual(start, tap.PressPosition);
			Assert.AreEqual(new Vector2(12f, 23f), tap.ReleasePosition);
			Assert.AreEqual(5.1, tap.TimeStamp);
			Assert.AreEqual(0.1, tap.TapDuration, 1e-9);
			Assert.AreEqual(gesture.TravelDistance, tap.TapDrift, 1e-4f);
		}
	}
}
