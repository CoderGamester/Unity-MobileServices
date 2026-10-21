using System.Text;
using GameLovers.MobileServices.GamePlatform.Internal;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.DeviceSimulatorPanel
{
	/// <summary>
	/// Device Simulator foldout that drives the process-wide Editor cloud save simulation every
	/// <c>CloudSaveService</c> in the Editor reads, so it acts on the game's own instance.
	/// </summary>
	internal static class CloudSaveSimulatorSection
	{
		public static VisualElement Build()
		{
			var foldout = new Foldout { text = "Cloud Save", value = false };
			foldout.AddToClassList("msp-foldout");

			var banner = new VisualElement();
			banner.AddToClassList("msp-banner");
			var bannerLabel = new Label("Saves from ICloudSaveService are stored under Library/GameLovers/SimulatedCloudSave. " +
				"Sign the simulated player in under Game Platform first; saves also need cloud storage available.");
			bannerLabel.AddToClassList("msp-banner-label");
			banner.Add(bannerLabel);
			foldout.Add(banner);

			var status = new Label();
			status.style.whiteSpace = WhiteSpace.Normal;
			status.style.fontSize = 10;
			status.style.marginTop = 4;

			var list = new Label();
			list.style.whiteSpace = WhiteSpace.Normal;
			list.style.fontSize = 10;
			list.style.marginTop = 4;

			var available = new Toggle("Cloud storage available") { value = EditorCloudSaveState.Available };
			available.RegisterValueChangedCallback(evt => EditorCloudSaveState.Available = evt.newValue);
			foldout.Add(available);

			var slot = new TextField("Slot") { value = "slot1" };
			foldout.Add(slot);
			var conflictData = new TextField("Conflicting data") { value = "saved on another device" };
			foldout.Add(conflictData);

			var inject = new Button(() =>
			{
				var injected = EditorCloudSaveState.InjectConflict(slot.value, Encoding.UTF8.GetBytes(conflictData.value));
				status.text = injected
					? $"Injected a newer conflicting version of '{slot.value}'. The next load resolves it by the conflict policy."
					: $"Slot '{slot.value}' has no save to conflict with; save it from the game first.";
			}) { text = "Inject conflicting version" };
			inject.style.marginTop = 2;
			foldout.Add(inject);

			var clear = new Button(() =>
			{
				EditorCloudSaveState.DeleteAll();
				Refresh();
			}) { text = "Delete all simulated saves" };
			clear.style.marginTop = 2;
			foldout.Add(clear);

			foldout.Add(list);
			foldout.Add(status);

			void Refresh()
			{
				available.SetValueWithoutNotify(EditorCloudSaveState.Available);
				var slots = EditorCloudSaveState.ListSlots();
				if (slots.Count == 0)
				{
					list.text = "No simulated saves.";
					return;
				}

				var builder = new StringBuilder("Saved slots:");
				foreach (var item in slots) builder.Append($"\n- {item.Name} ({item.ModifiedUtc.ToLocalTime():HH:mm:ss})");
				list.text = builder.ToString();
			}

			// The saves live on disk, so a light poll keeps the list current while the panel is open.
			foldout.schedule.Execute(Refresh).Every(1000);
			Refresh();
			return foldout;
		}
	}
}
