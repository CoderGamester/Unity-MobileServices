using System.Text;
using GameLovers.MobileServices.GamePlatform.Internal;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.DeviceSimulatorPanel
{
	/// <summary>Device Simulator foldout that drives the process-wide Editor cloud save simulation.</summary>
	internal sealed class CloudSaveSimulatorSection : Foldout
	{
		private readonly Toggle _available;
		private readonly Label _list;
		private readonly Label _status;

		public CloudSaveSimulatorSection()
		{
			text = "Cloud Save";
			value = false;
			AddToClassList("msp-foldout");

			var banner = new VisualElement();
			banner.AddToClassList("msp-banner");
			var bannerLabel = new Label("Saves from ICloudSaveService are stored under Library/GameLovers/SimulatedCloudSave. " +
				"Sign the simulated player in under Game Platform first; saves also need cloud storage available.");
			bannerLabel.AddToClassList("msp-banner-label");
			banner.Add(bannerLabel);
			Add(banner);

			_available = new Toggle("Cloud storage available");
			_available.RegisterValueChangedCallback(evt => EditorCloudSaveState.Available = evt.newValue);
			Add(_available);

			var slot = new TextField("Slot") { value = "slot1" };
			Add(slot);
			var conflictData = new TextField("Conflicting data") { value = "saved on another device" };
			Add(conflictData);

			var inject = new Button(() => InjectConflict(slot.value, conflictData.value)) { text = "Inject conflicting version" };
			inject.style.marginTop = 2;
			Add(inject);

			var clear = new Button(() =>
			{
				EditorCloudSaveState.DeleteAll();
				Refresh();
			}) { text = "Delete all simulated saves" };
			clear.style.marginTop = 2;
			Add(clear);

			_list = new Label();
			_list.style.whiteSpace = WhiteSpace.Normal;
			_list.style.fontSize = 10;
			_list.style.marginTop = 4;
			Add(_list);

			_status = new Label();
			_status.style.whiteSpace = WhiteSpace.Normal;
			_status.style.fontSize = 10;
			_status.style.marginTop = 4;
			Add(_status);

			schedule.Execute(Refresh).Every(1000);
			Refresh();
		}

		private void InjectConflict(string slot, string data)
		{
			var injected = EditorCloudSaveState.InjectConflict(slot, Encoding.UTF8.GetBytes(data));
			_status.text = injected
				? $"Injected a newer conflicting version of '{slot}'. The next load resolves it by the conflict policy."
				: $"Slot '{slot}' has no save to conflict with; save it from the game first.";
		}

		private void Refresh()
		{
			_available.SetValueWithoutNotify(EditorCloudSaveState.Available);
			var slots = EditorCloudSaveState.ListSlots();
			if (slots.Count == 0)
			{
				_list.text = "No simulated saves.";
				return;
			}

			var builder = new StringBuilder("Saved slots:");
			foreach (var item in slots) builder.Append($"\n- {item.Name} ({item.ModifiedUtc.ToLocalTime():HH:mm:ss})");
			_list.text = builder.ToString();
		}
	}
}
