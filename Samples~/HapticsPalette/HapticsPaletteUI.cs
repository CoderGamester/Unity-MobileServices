using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Haptics;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.HapticsPalette
{
	/// <summary>
	/// Designer-focused haptic exploration sample — preset grid + sequence recorder + replay.
	/// See the per-sample <c>README.md</c>.
	/// </summary>
	public sealed class HapticsPaletteUI : MonoBehaviour
	{
		private IHapticsService _haptics;
		private readonly List<(HapticPreset preset, float delay)> _sequence = new List<(HapticPreset, float)>();
		private float _lastTriggerTime;
		private Text _statusLabel;
		private Text _sequenceLabel;

		private void Awake()
		{
			_haptics = new HapticsService();
		}

		private void Start()
		{
			BuildUi();
		}

		private void OnDestroy()
		{
			_haptics?.StopCurrentHaptic();
		}

		private void BuildUi()
		{
			var canvasGo = new GameObject("HapticsCanvas");
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasGo.AddComponent<GraphicRaycaster>();

			var layoutGo = new GameObject("Layout", typeof(RectTransform), typeof(VerticalLayoutGroup));
			layoutGo.transform.SetParent(canvas.transform, false);
			var rt = (RectTransform)layoutGo.transform;
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = new Vector2(16, 16);
			rt.offsetMax = new Vector2(-16, -16);
			var v = layoutGo.GetComponent<VerticalLayoutGroup>();
			v.spacing = 8;
			v.childForceExpandHeight = false;
			v.childForceExpandWidth = true;

			AddHeader(layoutGo.transform, "Haptics Palette");
			_statusLabel = AddLabel(layoutGo.transform, $"IsSupported: {_haptics.IsSupported}");

			AddSectionHeader(layoutGo.transform, "Presets");
			var gridGo = new GameObject("PresetGrid", typeof(RectTransform), typeof(GridLayoutGroup));
			gridGo.transform.SetParent(layoutGo.transform, false);
			var grid = gridGo.GetComponent<GridLayoutGroup>();
			grid.cellSize = new Vector2(200, 60);
			grid.spacing = new Vector2(8, 8);
			grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			grid.constraintCount = 3;
			gridGo.AddComponent<LayoutElement>().minHeight = 220;

			foreach (HapticPreset preset in Enum.GetValues(typeof(HapticPreset)))
			{
				if (preset == HapticPreset.None) continue;
				var captured = preset;
				AddButton(gridGo.transform, preset.ToString(), () =>
				{
					var now = Time.realtimeSinceStartup;
					var delay = _lastTriggerTime > 0 ? now - _lastTriggerTime : 0f;
					_lastTriggerTime = now;
					_haptics.PlayPreset(captured);
					_sequence.Add((captured, delay));
					if (_sequence.Count > 16) _sequence.RemoveAt(0);
					UpdateSequenceLabel();
					_statusLabel.text = $"Last: {captured}";
				});
			}

			AddSectionHeader(layoutGo.transform, "Recorded sequence");
			_sequenceLabel = AddLabel(layoutGo.transform, "(empty)");

			var ctrlRow = new GameObject("Controls", typeof(RectTransform), typeof(HorizontalLayoutGroup));
			ctrlRow.transform.SetParent(layoutGo.transform, false);
			var hRow = ctrlRow.GetComponent<HorizontalLayoutGroup>();
			hRow.spacing = 8;
			ctrlRow.AddComponent<LayoutElement>().minHeight = 40;

			AddButton(ctrlRow.transform, "Replay", ReplaySequence);
			AddButton(ctrlRow.transform, "Clear", () =>
			{
				_sequence.Clear();
				UpdateSequenceLabel();
			});
			AddButton(ctrlRow.transform, "Stop", () =>
			{
				_haptics.StopCurrentHaptic();
				_statusLabel.text = "Stopped.";
			});
		}

		private void UpdateSequenceLabel()
		{
			if (_sequence.Count == 0)
			{
				_sequenceLabel.text = "(empty)";
				return;
			}
			var sb = new System.Text.StringBuilder();
			for (var i = 0; i < _sequence.Count; i++)
			{
				sb.Append(_sequence[i].preset);
				sb.Append("(+").Append(_sequence[i].delay.ToString("F2")).Append("s) ");
			}
			_sequenceLabel.text = sb.ToString();
		}

		private void ReplaySequence()
		{
			if (_sequence.Count == 0) return;
			StartCoroutine(ReplayCoroutine());
		}

		private System.Collections.IEnumerator ReplayCoroutine()
		{
			foreach (var (preset, delay) in _sequence)
			{
				if (delay > 0f)
				{
					yield return new WaitForSecondsRealtime(delay);
				}
				_haptics.PlayPreset(preset);
				_statusLabel.text = $"Replay: {preset}";
			}
			_statusLabel.text = "Replay finished.";
		}

		// ---- UI helpers (duplicated to keep each sample self-contained) ----

		private static void AddHeader(Transform parent, string text)
		{
			var t = AddLabel(parent, text);
			t.fontSize = 22;
			t.fontStyle = FontStyle.Bold;
		}

		private static void AddSectionHeader(Transform parent, string text)
		{
			var t = AddLabel(parent, text);
			t.fontSize = 16;
			t.fontStyle = FontStyle.Bold;
			t.color = new Color(0.8f, 0.9f, 1f);
		}

		private static Text AddLabel(Transform parent, string text)
		{
			var go = new GameObject("Label", typeof(Text));
			go.transform.SetParent(parent, false);
			var t = go.GetComponent<Text>();
			t.text = text;
			t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			t.fontSize = 13;
			t.color = Color.white;
			t.alignment = TextAnchor.UpperLeft;
			go.AddComponent<LayoutElement>().minHeight = 18;
			return t;
		}

		private static Button AddButton(Transform parent, string label, Action onClick)
		{
			var go = new GameObject(label, typeof(Image), typeof(Button));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.6f, 0.85f);
			var btn = go.GetComponent<Button>();
			btn.onClick.AddListener(() => onClick?.Invoke());

			var le = go.AddComponent<LayoutElement>();
			le.minHeight = 36;

			var textGo = new GameObject("Text", typeof(Text));
			textGo.transform.SetParent(go.transform, false);
			var rt = (RectTransform)textGo.transform;
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			var t = textGo.GetComponent<Text>();
			t.text = label;
			t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			t.fontSize = 13;
			t.color = Color.white;
			t.alignment = TextAnchor.MiddleCenter;

			return btn;
		}
	}
}
