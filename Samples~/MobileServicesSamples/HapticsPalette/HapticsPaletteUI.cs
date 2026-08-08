using System;
using System.Collections;
using System.Collections.Generic;
using GameLovers.MobileServices.Haptics;
using GameLovers.MobileServices.Samples;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.HapticsPalette
{
	/// <summary>Explore every preset, duration mode, custom intensity and sequence replay.</summary>
	public sealed class HapticsPaletteUI : MonoBehaviour
	{
		private struct RecordedHaptic
		{
			public readonly HapticPreset Preset;
			public readonly float Delay;

			public RecordedHaptic(HapticPreset preset, float delay)
			{
				Preset = preset;
				Delay = delay;
			}
		}

		private UIDocument _document;
		private IHapticsService _haptics;
		private readonly List<RecordedHaptic> _recording = new List<RecordedHaptic>();
		private Coroutine _replay;
		private float _lastPlayTime;
		private VisualElement _boundRoot;
		private Label _status;
		private Label _recordingLabel;
		private string _lastStatusMessage = "Ready";

		private void Awake()
		{
			_document = GetComponent<UIDocument>();
			EnsureRuntimeDependencies();
		}

		private void Start()
		{
			EnsureUiBound();
		}

		private void Update()
		{
			EnsureUiBound();
		}

		private void OnDestroy()
		{
			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			if (_replay != null) StopCoroutine(_replay);
			_haptics?.StopCurrentHaptic();
			(_haptics as IDisposable)?.Dispose();
		}

		private void EnsureUiBound()
		{
			EnsureRuntimeDependencies();
			if (_document == null) _document = GetComponent<UIDocument>();
			var root = _document == null ? null : _document.rootVisualElement;
			if (root == null) return;
			var status = root.Q<Label>("status");
			if (ReferenceEquals(_status, status)) return;

			_boundRoot?.UnregisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
			_boundRoot = root;
			_status = status;
			_recordingLabel = root.Q<Label>("recording");
			BindClickHaptics(root);
			BindPresets(root);
			root.Q<Button>("custom-play")?.RegisterCallback<ClickEvent>(_ => PlayCustom(root));
			root.Q<Button>("stop")?.RegisterCallback<ClickEvent>(_ => StopHaptic());
			root.Q<Button>("replay")?.RegisterCallback<ClickEvent>(_ => Replay());
			root.Q<Button>("clear")?.RegisterCallback<ClickEvent>(_ =>
			{
				_recording.Clear();
				RefreshRecording();
				RefreshStatus("Recording cleared");
			});
			RefreshStatus(_lastStatusMessage);
			RefreshRecording();
		}

		private void EnsureRuntimeDependencies()
		{
			if (_haptics == null) _haptics = new HapticsService();
		}

		private void BindClickHaptics(VisualElement root)
		{
			root.RegisterCallback<ClickEvent>(OnButtonClick, TrickleDown.TrickleDown);
		}

		private void OnButtonClick(ClickEvent evt)
		{
			var target = evt.target as VisualElement;
			var button = target as Button ?? target?.GetFirstAncestorOfType<Button>();
			if (button != null && button.enabledInHierarchy && !IsHapticAction(button))
			{
				_haptics.PlayPreset(HapticPreset.Selection);
			}
		}

		private static bool IsHapticAction(Button button)
		{
			if (button.name == "custom-play" || button.name == "replay") return true;
			for (var parent = button.parent; parent != null; parent = parent.parent)
			{
				if (parent.name == "preset-list") return true;
			}
			return false;
		}

		private void BindPresets(VisualElement root)
		{
			var list = root.Q<VisualElement>("preset-list");
			if (list == null) return;
			list.Clear();
			foreach (HapticPreset preset in Enum.GetValues(typeof(HapticPreset)))
			{
				if (preset == HapticPreset.None) continue;
				var captured = preset;
				var row = new VisualElement();
				row.AddToClassList("preset-row");
				row.Add(new Label(preset.ToString()));
				var natural = new Button(() => Play(captured, 0f)) { text = "Natural" };
				natural.AddToClassList("sample-button");
				row.Add(natural);
				var finite = new Button(() => Play(captured, 0.6f)) { text = "0.6s" };
				finite.AddToClassList("sample-button");
				row.Add(finite);
				var indefinite = new Button(() => Play(captured, -1f)) { text = "Indefinite" };
				indefinite.AddToClassList("sample-button");
				row.Add(indefinite);
				list.Add(row);
			}
		}

		private void Play(HapticPreset preset, float duration)
		{
			if (_replay != null) StopCoroutine(_replay);
			_replay = null;
			var now = Time.realtimeSinceStartup;
			var delay = _lastPlayTime > 0f ? now - _lastPlayTime : 0f;
			_lastPlayTime = now;
			_haptics.PlayPresetDuration(preset, duration);
			_recording.Add(new RecordedHaptic(preset, delay));
			if (_recording.Count > 24) _recording.RemoveAt(0);
			RefreshRecording();
			RefreshStatus($"Playing {preset} ({DescribeDuration(duration)})");
		}

		private void PlayCustom(VisualElement root)
		{
			var intensity = root.Q<Slider>("intensity")?.value ?? 0.8f;
			var durationMs = root.Q<SliderInt>("duration-ms")?.value ?? 250;
			_haptics.PlayCustom(intensity, durationMs);
			RefreshStatus($"Custom intensity {intensity:P0}, {durationMs}ms");
		}

		private void StopHaptic()
		{
			if (_replay != null) StopCoroutine(_replay);
			_replay = null;
			_haptics.StopCurrentHaptic();
			RefreshStatus("Stopped");
		}

		private void Replay()
		{
			if (_replay != null || _recording.Count == 0)
			{
				RefreshStatus(_replay != null ? "Replay already running" : "Nothing recorded");
				return;
			}
			// Copy the immutable values so pressing Clear during replay cannot mutate this run.
			var snapshot = new List<RecordedHaptic>(_recording);
			_replay = StartCoroutine(ReplaySnapshot(snapshot));
		}

		private IEnumerator ReplaySnapshot(IReadOnlyList<RecordedHaptic> snapshot)
		{
			RefreshStatus("Replay running");
			for (var i = 0; i < snapshot.Count; i++)
			{
				var delay = snapshot[i].Delay;
				if (delay > 0f) yield return new WaitForSecondsRealtime(Mathf.Min(delay, 2f));
				_haptics.PlayPreset(snapshot[i].Preset);
				RefreshStatus($"Replay {i + 1}/{snapshot.Count}: {snapshot[i].Preset}");
			}
			_replay = null;
			RefreshStatus("Replay finished");
		}

		private void RefreshStatus(string message)
		{
			_lastStatusMessage = message;
			if (_status == null) return;
			_status.text = SampleStatusFormatter.Format(
				new SampleStatusEntry("Haptics supported", SampleStatusFormatter.YesNo(_haptics?.IsSupported == true)),
				new SampleStatusEntry("Haptics enabled", SampleStatusFormatter.YesNo(_haptics?.Enabled == true)),
				new SampleStatusEntry("Haptics playing", SampleStatusFormatter.YesNo(_haptics?.IsPlaying == true)),
				new SampleStatusEntry("Last action", message));
		}

		private void RefreshRecording()
		{
			if (_recordingLabel == null) return;
			if (_recording.Count == 0)
			{
				_recordingLabel.text = "(empty)";
				return;
			}
			var lines = new List<string>(_recording.Count);
			foreach (var item in _recording) lines.Add($"{item.Preset} (+{item.Delay:F2}s)");
			_recordingLabel.text = string.Join("  →  ", lines);
		}

		private static string DescribeDuration(float duration) => duration < 0f ? "indefinite" : duration == 0f ? "natural" : $"{duration:F1}s";

	}
}
