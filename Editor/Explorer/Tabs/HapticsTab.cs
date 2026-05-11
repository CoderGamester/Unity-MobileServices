using System;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Haptics.Internal;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Haptics tab — preset buttons + custom intensity + per-preset envelope graph.</summary>
	public sealed class HapticsTab : MobileServiceTab
	{
		public override string DisplayName => "Haptics";
		protected override int RefreshIntervalMs => 500;

		private HapticsService _haptics;
		private HapticPreset _previewPreset = HapticPreset.Selection;
		private Label _statusLabel;
		private VisualElement _envelopeCanvas;
		private Slider _intensitySlider;
		private FloatField _durationField;
		private Label _intensityValueLabel;
		private Label _durationValueLabel;

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			_statusLabel = new Label();
			scroll.Add(_statusLabel);

			scroll.Add(MakeSectionLabel("Preset"));
			var presetGrid = new VisualElement();
			presetGrid.style.flexDirection = FlexDirection.Row;
			presetGrid.style.flexWrap = Wrap.Wrap;
			foreach (HapticPreset preset in Enum.GetValues(typeof(HapticPreset)))
			{
				if (preset == HapticPreset.None) continue;
				var captured = preset;
				var btn = new Button(() =>
				{
					_previewPreset = captured;
					EnsureHaptics().PlayPreset(captured);
					RebuildEnvelope();
					RefreshStatus();
				}) { text = preset.ToString() };
				btn.style.minWidth = 80;
				btn.style.marginRight = 4;
				btn.style.marginBottom = 4;
				presetGrid.Add(btn);
			}
			scroll.Add(presetGrid);

			scroll.Add(MakeSectionLabel("Envelope (timings ms × amplitudes 0..255)"));
			_envelopeCanvas = new VisualElement();
			_envelopeCanvas.AddToClassList("haptic-envelope-canvas");
			scroll.Add(_envelopeCanvas);

			scroll.Add(MakeSectionLabel("Custom"));
			// Use a side-mounted label + naked slider so the label width pitfall doesn't eat space.
			var intensityRow = new VisualElement();
			intensityRow.style.flexDirection = FlexDirection.Row;
			intensityRow.style.alignItems = Align.Center;
			intensityRow.Add(new Label("Intensity (0..1)"));
			_intensitySlider = new Slider(0f, 1f) { value = 0.7f };
			_intensitySlider.style.flexGrow = 1;
			_intensitySlider.style.marginLeft = 8;
			_intensityValueLabel = new Label("0.70");
			_intensityValueLabel.style.minWidth = 40;
			_intensityValueLabel.style.marginLeft = 6;
			_intensitySlider.RegisterValueChangedCallback(evt => _intensityValueLabel.text = evt.newValue.ToString("F2"));
			intensityRow.Add(_intensitySlider);
			intensityRow.Add(_intensityValueLabel);
			scroll.Add(intensityRow);

			var durationRow = new VisualElement();
			durationRow.style.flexDirection = FlexDirection.Row;
			durationRow.style.alignItems = Align.Center;
			durationRow.Add(new Label("Duration (ms)"));
			_durationField = new FloatField { value = 250f };
			_durationField.style.flexGrow = 1;
			_durationField.style.marginLeft = 8;
			_durationValueLabel = new Label();
			_durationValueLabel.style.minWidth = 50;
			_durationValueLabel.style.marginLeft = 6;
			_durationField.RegisterValueChangedCallback(evt => _durationValueLabel.text = $"{evt.newValue:F0} ms");
			_durationValueLabel.text = $"{_durationField.value:F0} ms";
			durationRow.Add(_durationField);
			durationRow.Add(_durationValueLabel);
			scroll.Add(durationRow);

			var playCustomBtn = new Button(() =>
			{
				if (!Application.isPlaying)
				{
					Debug.Log("[MobileServicesExplorer] PlayCustom requires Play mode — HapticsHost spawns a DontDestroyOnLoad GameObject.");
					return;
				}
				EnsureHaptics().PlayCustom(_intensitySlider.value, _durationField.value);
				_previewPreset = HapticPreset.None;
				RebuildEnvelope();
				RefreshStatus();
			}) { text = "Play Custom (Play mode only)" };
			playCustomBtn.AddToClassList("action-primary");
			scroll.Add(playCustomBtn);

			scroll.Add(MakeSectionLabel("Looped"));
			var loopRow = new VisualElement();
			loopRow.style.flexDirection = FlexDirection.Row;
			loopRow.Add(new Button(() =>
			{
				if (!Application.isPlaying)
				{
					Debug.Log("[MobileServicesExplorer] Indefinite loop requires Play mode.");
					return;
				}
				EnsureHaptics().PlayPresetDuration(_previewPreset, -1f);
				RefreshStatus();
			}) { text = "Loop (until stop)" });
			loopRow.Add(new Button(() =>
			{
				if (!Application.isPlaying)
				{
					Debug.Log("[MobileServicesExplorer] Timed loop requires Play mode.");
					return;
				}
				EnsureHaptics().PlayPresetDuration(_previewPreset, 0.5f);
				RefreshStatus();
			}) { text = "Loop 500ms" });
			scroll.Add(loopRow);

			var bar = MakeActionBar();
			bar.Add(MakePrimaryDangerButton("Stop", () =>
			{
				_haptics?.StopCurrentHaptic();
				RefreshStatus();
			}));
			scroll.Add(bar);

			Add(scroll);
			RebuildEnvelope();
			RefreshStatus();
		}

		protected override void Refresh()
		{
			RefreshStatus();
		}

		protected override void OnExitingPlayMode()
		{
			_haptics?.StopCurrentHaptic();
			_haptics = null;
		}

		private HapticsService EnsureHaptics()
		{
			return _haptics ??= new HapticsService();
		}

		private void RefreshStatus()
		{
			if (_haptics == null)
			{
				_statusLabel.text = "Haptics: (none)";
				return;
			}
			_statusLabel.text = $"Haptics: IsPlaying={_haptics.IsPlaying}, CurrentPreset={_haptics.CurrentPreset}, Duration={_haptics.CurrentDurationSeconds:F2}s, IsSupported={_haptics.IsSupported}";
		}

		private void RebuildEnvelope()
		{
			_envelopeCanvas.Clear();
			var (timesSec, amps) = HapticEnvelopes.GetFloatEnvelopeFor(_previewPreset);
			if (timesSec == null || timesSec.Length == 0)
			{
				return;
			}

			var totalSec = 0f;
			for (var i = 0; i < timesSec.Length; i++)
			{
				totalSec += timesSec[i];
			}
			if (totalSec <= 0f)
			{
				return;
			}

			var row = new VisualElement();
			row.style.flexDirection = FlexDirection.Row;
			row.style.alignItems = Align.FlexEnd;
			row.style.flexGrow = 1;
			row.style.height = Length.Percent(100);
			_envelopeCanvas.Add(row);

			for (var i = 0; i < timesSec.Length; i++)
			{
				var widthPct = timesSec[i] / totalSec * 100f;
				var bar = new VisualElement();
				bar.AddToClassList("haptic-bar");
				bar.style.width = Length.Percent(widthPct);
				bar.style.height = Length.Percent(Mathf.Max(2f, amps[i] * 100f));
				bar.style.marginRight = 1;
				bar.tooltip = $"{timesSec[i] * 1000f:F0} ms @ {amps[i]:F3} amp";
				row.Add(bar);
			}
		}
	}
}
