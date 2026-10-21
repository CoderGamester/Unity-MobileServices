using GameLovers.MobileServices.GamePlatform.Internal;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.DeviceSimulatorPanel
{
	/// <summary>Device Simulator foldout that drives the process-wide Editor game-service simulation.</summary>
	internal sealed class GamePlatformSimulatorSection : Foldout
	{
		private readonly Toggle _signedIn;
		private readonly Toggle _signInSucceeds;
		private readonly TextField _playerName;
		private readonly Label _activity;

		public GamePlatformSimulatorSection()
		{
			text = "Game Platform";
			value = false;
			AddToClassList("msp-foldout");

			var banner = new VisualElement();
			banner.AddToClassList("msp-banner");
			var bannerLabel = new Label("Simulates Game Center and Google Play Games for GamePlatformServices in the Editor. " +
				"Sign-in, scores and achievements are recorded below instead of reaching a platform.");
			bannerLabel.AddToClassList("msp-banner-label");
			banner.Add(bannerLabel);
			Add(banner);

			_signedIn = new Toggle("Signed in");
			_signedIn.RegisterValueChangedCallback(evt =>
			{
				if (evt.newValue != EditorGamePlatformState.SignedIn) EditorGamePlatformState.SetSignedIn(evt.newValue);
			});
			Add(_signedIn);

			_signInSucceeds = new Toggle("Next sign-in succeeds");
			_signInSucceeds.RegisterValueChangedCallback(evt => EditorGamePlatformState.SignInSucceeds = evt.newValue);
			Add(_signInSucceeds);

			_playerName = new TextField("Player name");
			_playerName.RegisterValueChangedCallback(evt => EditorGamePlatformState.PlayerName = evt.newValue);
			Add(_playerName);

			_activity = new Label();
			_activity.style.whiteSpace = WhiteSpace.Normal;
			_activity.style.fontSize = 10;
			_activity.style.marginTop = 4;
			Add(_activity);

			var reset = new Button(EditorGamePlatformState.Reset) { text = "Sign out and clear activity" };
			reset.style.marginTop = 2;
			Add(reset);

			RegisterCallback<AttachToPanelEvent>(_ =>
			{
				EditorGamePlatformState.Changed += Refresh;
				Refresh();
			});
			RegisterCallback<DetachFromPanelEvent>(_ => EditorGamePlatformState.Changed -= Refresh);
			Refresh();
		}

		private void Refresh()
		{
			_signedIn.SetValueWithoutNotify(EditorGamePlatformState.SignedIn);
			_signInSucceeds.SetValueWithoutNotify(EditorGamePlatformState.SignInSucceeds);
			_playerName.SetValueWithoutNotify(EditorGamePlatformState.PlayerName);
			_activity.text = EditorGamePlatformState.Activity.Count == 0
				? "No game-service activity yet."
				: "Recent activity:\n- " + string.Join("\n- ", EditorGamePlatformState.Activity);
		}
	}
}
