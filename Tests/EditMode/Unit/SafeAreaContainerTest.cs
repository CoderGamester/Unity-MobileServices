using System;
using GameLovers.MobileServices.Device;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class SafeAreaContainerTest
	{
		[Test]
		// ADMIT: SafeAreaContainer.SetSafeAreaService could subscribe without applying the current safe area, leaving the container unpadded until the next change.
		// RCR: SafeAreaContainer.cs SetSafeAreaService — drop the trailing `Apply()` → RED (paddingLeft expected 10 was 0).
		public void SetSafeAreaService_AppliesPaddingFromCurrentSafeArea()
		{
			var fake = new FakeSafeAreaService(new Rect(10f, 20f, Screen.width - 30f, Screen.height - 50f));
			var container = new SafeAreaContainer();

			container.SetSafeAreaService(fake);

			AssertPaddingMatches(container, fake.SafeArea);
		}

		[Test]
		// ADMIT: SafeAreaContainer could stop re-applying padding when the service raises OnSafeAreaChanged, so rotation leaves stale insets.
		// RCR: SafeAreaContainer.cs OnSafeAreaChanged — empty the handler body (drop `Apply()`) → RED (paddingLeft expected 15 was 0).
		public void SetSafeAreaService_OnSafeAreaChanged_UpdatesPadding()
		{
			var fake = new FakeSafeAreaService(new Rect(0f, 0f, Screen.width, Screen.height));
			var container = new SafeAreaContainer(fake);

			var newArea = new Rect(15f, 25f, Screen.width - 40f, Screen.height - 60f);
			fake.RaiseChanged(newArea);

			AssertPaddingMatches(container, newArea);
		}

		[Test]
		// ADMIT: SafeAreaContainer.SetSafeAreaService could leave the previous service subscribed, double-applying padding after a swap.
		// RCR: SafeAreaContainer.cs SetSafeAreaService — drop `_safeAreaService.OnSafeAreaChanged -= OnSafeAreaChanged` → RED (first.HandlerCount expected 0 was 1).
		public void SetSafeAreaService_Replace_UnsubscribesPreviousService()
		{
			var first = new FakeSafeAreaService(new Rect(0f, 0f, Screen.width, Screen.height));
			var second = new FakeSafeAreaService(new Rect(5f, 5f, Screen.width - 10f, Screen.height - 10f));

			var container = new SafeAreaContainer(first);
			container.SetSafeAreaService(second);

			Assert.AreEqual(0, first.HandlerCount);
			Assert.AreEqual(1, second.HandlerCount);
		}

		private static void AssertPaddingMatches(SafeAreaContainer container, Rect safeArea)
		{
			var screenWidth = Screen.width;
			var screenHeight = Screen.height;
			if (screenWidth <= 0 || screenHeight <= 0)
			{
				Assert.Inconclusive("Screen dimensions are not initialised in this EditMode harness; padding assertion skipped.");
				return;
			}

			Assert.AreEqual(safeArea.xMin, container.style.paddingLeft.value.value, 1e-3f);
			Assert.AreEqual(screenWidth - safeArea.xMax, container.style.paddingRight.value.value, 1e-3f);
			Assert.AreEqual(screenHeight - safeArea.yMax, container.style.paddingTop.value.value, 1e-3f);
			Assert.AreEqual(safeArea.yMin, container.style.paddingBottom.value.value, 1e-3f);
		}

		private sealed class FakeSafeAreaService : ISafeAreaService
		{
			public int HandlerCount;

			public FakeSafeAreaService(Rect safeArea)
			{
				SafeArea = safeArea;
			}

			public Rect SafeArea { get; private set; }

			private event Action<Rect> _onChanged;

			public event Action<Rect> OnSafeAreaChanged
			{
				add { _onChanged += value; HandlerCount++; }
				remove { _onChanged -= value; HandlerCount--; }
			}

			public void RaiseChanged(Rect newArea)
			{
				SafeArea = newArea;
				_onChanged?.Invoke(newArea);
			}
		}
	}
}
