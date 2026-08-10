using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// UI Toolkit container that automatically pads its content to respect the device safe area.
	/// Subscribe via the constructor that accepts an <see cref="ISafeAreaService"/>; the container
	/// updates its own padding whenever the safe area changes.
	/// </summary>
	[UxmlElement]
	public sealed partial class SafeAreaContainer : VisualElement
	{
		private ISafeAreaService _safeAreaService;

		public SafeAreaContainer()
		{
			RegisterCallback<GeometryChangedEvent>(_ => Apply());
		}

		public SafeAreaContainer(ISafeAreaService safeAreaService) : this()
		{
			SetSafeAreaService(safeAreaService);
		}

		/// <summary>
		/// Wires the container to the supplied service and applies the current safe area immediately.
		/// </summary>
		public void SetSafeAreaService(ISafeAreaService safeAreaService)
		{
			if (_safeAreaService != null)
			{
				_safeAreaService.OnSafeAreaChanged -= OnSafeAreaChanged;
			}

			_safeAreaService = safeAreaService;

			if (_safeAreaService == null)
			{
				return;
			}

			_safeAreaService.OnSafeAreaChanged += OnSafeAreaChanged;
			Apply();
		}

		/// <summary>Converts a screen-pixel inset into the active UI Toolkit panel coordinate system.</summary>
		internal static float ScreenPixelsToPanelUnits(float screenPixels, float scaledPixelsPerPoint)
		{
			if (scaledPixelsPerPoint <= 0f)
			{
				return screenPixels;
			}

			return screenPixels / scaledPixelsPerPoint;
		}

		private void OnSafeAreaChanged(Rect _)
		{
			Apply();
		}

		private void Apply()
		{
			var safeArea = _safeAreaService?.SafeArea ?? Screen.safeArea;
			var screenWidth = Screen.width;
			var screenHeight = Screen.height;
			if (screenWidth <= 0 || screenHeight <= 0)
			{
				return;
			}

			// Screen.safeArea is expressed in screen pixels, while UI Toolkit styles use the
			// panel's coordinate system. scaledPixelsPerPoint is Unity's authoritative conversion,
			// including the PanelSettings Scale With Screen Size factor.
			var scaledPixelsPerPoint = panel?.scaledPixelsPerPoint ?? 1f;
			var left   = ScreenPixelsToPanelUnits(safeArea.xMin, scaledPixelsPerPoint);
			var right  = ScreenPixelsToPanelUnits(screenWidth - safeArea.xMax, scaledPixelsPerPoint);
			var top    = ScreenPixelsToPanelUnits(screenHeight - safeArea.yMax, scaledPixelsPerPoint);
			var bottom = ScreenPixelsToPanelUnits(safeArea.yMin, scaledPixelsPerPoint);

			style.paddingLeft   = left;
			style.paddingRight  = right;
			style.paddingTop    = top;
			style.paddingBottom = bottom;
		}
	}
}
