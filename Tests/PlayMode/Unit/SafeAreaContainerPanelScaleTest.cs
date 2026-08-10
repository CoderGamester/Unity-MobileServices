using System;
using System.Collections;
using GameLovers.MobileServices.Device;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class SafeAreaContainerPanelScaleTest
	{
		[UnityTest]
		// ADMIT: A scaled UI Toolkit panel could apply screen-pixel insets directly as panel units,
		// causing a notch inset to be multiplied by the panel scale.
		// RCR: SafeAreaContainer.cs Apply — assign screen pixels directly instead of dividing by scaledPixelsPerPoint → RED (padding is too large).
		public IEnumerator SetSafeAreaService_ScaledPanel_UsesPanelCoordinatesForScreenInset()
		{
			var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			panelSettings.referenceResolution = new Vector2Int(720, 1280);
			var documentObject = new GameObject("SafeAreaContainerPanelScaleTest");
			var document = documentObject.AddComponent<UIDocument>();
			document.panelSettings = panelSettings;
			var safeArea = new FakeSafeAreaService(new Rect(92f, 0f, Screen.width - 92f, Screen.height));
			var container = new SafeAreaContainer(safeArea) { style = { flexGrow = 1f } };
			document.rootVisualElement.Add(container);

			yield return null;
			safeArea.RaiseChanged(safeArea.SafeArea);
			yield return null;

			Assert.NotNull(container.panel);
			var expectedPadding = 92f / container.panel.scaledPixelsPerPoint;
			Assert.AreEqual(expectedPadding, container.style.paddingLeft.value.value, 0.01f);

			UnityEngine.Object.Destroy(documentObject);
			UnityEngine.Object.Destroy(panelSettings);
		}

		private sealed class FakeSafeAreaService : ISafeAreaService
		{
			public Rect SafeArea { get; }

			public event Action<Rect> OnSafeAreaChanged;

			public FakeSafeAreaService(Rect safeArea) => SafeArea = safeArea;

			public void RaiseChanged(Rect safeArea) => OnSafeAreaChanged?.Invoke(safeArea);
		}
	}
}
