using System;
using System.Collections.Generic;
using GameLovers.MobileServices.Device;
using GameLovers.MobileServices.Editor.Explorer.Overlays;
using GameLovers.MobileServices.Editor.Simulation;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.Tabs
{
	/// <summary>Permissions tab — per-permission status pill + Check/Request + "Simulate next" dropdown.</summary>
	public sealed class PermissionsTab : MobileServiceTab
	{
		public override string DisplayName => "Permissions";
		protected override int RefreshIntervalMs => 1000;

		private readonly PermissionsService _service = new PermissionsService();
		private readonly Dictionary<AppPermission, Label> _statusPills = new Dictionary<AppPermission, Label>();
		private readonly Dictionary<AppPermission, DropdownField> _resultDropdowns =
			new Dictionary<AppPermission, DropdownField>();

		protected override void BuildUi()
		{
			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.AddToClassList("tab-scroll");

			scroll.Add(MakeSectionLabel("Permissions"));

			foreach (AppPermission p in Enum.GetValues(typeof(AppPermission)))
			{
				scroll.Add(BuildPermissionRow(p));
			}

			scroll.Add(MakeSectionLabel("Notes"));
			var note = new Label(
				"Editor short-circuits Check/Request to Granted by default. The 'Simulate next' dropdown lets you queue an alternative result for the next RequestAsync call. The Mobile Simulator window also paints the platform-shaped permission dialog with the configured usage description.");
			note.style.whiteSpace = WhiteSpace.Normal;
			note.AddToClassList("tab-empty-label");
			scroll.Add(note);

			Add(scroll);
		}

		protected override void Refresh()
		{
			var snapshot = _service.CheckSnapshot();
			foreach (var kv in snapshot)
			{
				if (_statusPills.TryGetValue(kv.Key, out var pill))
				{
					ApplyStatusPill(pill, kv.Value);
				}
			}
		}

		private VisualElement BuildPermissionRow(AppPermission permission)
		{
			var row = new VisualElement();
			row.AddToClassList("row");

			var lbl = new Label(permission.ToString());
			lbl.AddToClassList("row-label");
			row.Add(lbl);

			var pill = new Label();
			ApplyStatusPill(pill, _service.Check(permission));
			_statusPills[permission] = pill;
			row.Add(pill);

			row.Add(MakeRowButton("Check", () =>
			{
				var status = _service.Check(permission);
				ApplyStatusPill(pill, status);
			}));

			row.Add(MakeRowButton("Request", () =>
			{
				_ = RequestAsyncDelegate(permission, pill);
			}));

			var dropdown = new DropdownField(new List<string>
			{
				"(no override)",
				PermissionStatus.Granted.ToString(),
				PermissionStatus.Denied.ToString(),
				PermissionStatus.NotDetermined.ToString(),
				PermissionStatus.Restricted.ToString(),
			}, 0);
			dropdown.RegisterValueChangedCallback(evt =>
			{
				if (evt.newValue == "(no override)")
				{
					EditorPlatformSimulator.QueuePermissionResult(permission, null);
				}
				else if (Enum.TryParse<PermissionStatus>(evt.newValue, out var parsed))
				{
					EditorPlatformSimulator.QueuePermissionResult(permission, parsed);
				}
			});
			dropdown.style.minWidth = 130;
			dropdown.style.marginLeft = 6;
			_resultDropdowns[permission] = dropdown;
			row.Add(dropdown);

			row.Add(MakeRowButton("Show Mock", () =>
			{
				MobileSimulatorState.PushPermissionDialog(new SimulatedPermissionDialogSpec
				{
					TypeName = permission.ToString(),
					UsageDescription = $"(set NSUsageDescription for {permission} in Project Settings)",
					IsAtt = false,
				});
			}));

			return row;
		}

		private async System.Threading.Tasks.Task RequestAsyncDelegate(AppPermission permission, Label pill)
		{
			var result = await _service.RequestAsync(permission);
			ApplyStatusPill(pill, result);
		}

		private static void ApplyStatusPill(Label pill, PermissionStatus status)
		{
			pill.text = status.ToString();
			pill.RemoveFromClassList("perm-pill-granted");
			pill.RemoveFromClassList("perm-pill-denied");
			pill.RemoveFromClassList("perm-pill-undetermined");
			pill.RemoveFromClassList("perm-pill-restricted");
			switch (status)
			{
				case PermissionStatus.Granted:      pill.AddToClassList("perm-pill-granted"); break;
				case PermissionStatus.Denied:       pill.AddToClassList("perm-pill-denied"); break;
				case PermissionStatus.Restricted:   pill.AddToClassList("perm-pill-restricted"); break;
				default:                            pill.AddToClassList("perm-pill-undetermined"); break;
			}
		}
	}
}
