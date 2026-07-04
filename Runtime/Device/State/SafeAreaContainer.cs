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

		/// <summary>Default constructor for UXML usage. Set the service via <see cref="SetSafeAreaService"/>.</summary>
		public SafeAreaContainer()
		{
			RegisterCallback<GeometryChangedEvent>(_ => Apply());
		}

		/// <summary>Code-construction with the service injected.</summary>
		public SafeAreaContainer(ISafeAreaService safeAreaService) : this()
		{
			SetSafeAreaService(safeAreaService);
		}

		/// <summary>Wires the container to the supplied service and applies the current safe area immediately.</summary>
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

			// Convert from screen pixels to UI Toolkit padding (top-left origin).
			var left   = safeArea.xMin;
			var right  = screenWidth  - safeArea.xMax;
			var top    = screenHeight - safeArea.yMax;
			var bottom = safeArea.yMin;

			style.paddingLeft   = left;
			style.paddingRight  = right;
			style.paddingTop    = top;
			style.paddingBottom = bottom;
		}
	}
}
