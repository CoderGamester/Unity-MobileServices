using GameLovers.MobileServices.GamePlatform.Internal;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Editor.Explorer.DeviceSimulatorPanel
{
	/// <summary>
	/// Device Simulator foldout that drives the process-wide Editor game-service simulation every
	/// <c>GamePlatformServices</c> in the Editor reads, so it acts on the game's own instance.
	/// </summary>
	internal static class GamePlatformSimulatorSection
	{
		public static VisualElement Build()
		{
			var foldout = new Foldout { text = "Game Platform", value = false };
			foldout.AddToClassList("msp-foldout");

			var banner = new VisualElement();
			banner.AddToClassList("msp-banner");
			var bannerLabel = new Label("Simulates Game Center and Google Play Games for GamePlatformServices in the Editor. " +
				"Sign-in, scores and achievements are recorded below instead of reaching a platform.");
			bannerLabel.AddToClassList("msp-banner-label");
			banner.Add(bannerLabel);
			foldout.Add(banner);

			var signedIn = new Toggle("Signed in") { value = EditorGamePlatformState.SignedIn };
			signedIn.RegisterValueChangedCallback(evt =>
			{
				if (evt.newValue != EditorGamePlatformState.SignedIn) EditorGamePlatformState.SetSignedIn(evt.newValue);
			});
			foldout.Add(signedIn);

			var succeeds = new Toggle("Next sign-in succeeds") { value = EditorGamePlatformState.SignInSucceeds };
			succeeds.RegisterValueChangedCallback(evt => EditorGamePlatformState.SignInSucceeds = evt.newValue);
			foldout.Add(succeeds);

			var playerName = new TextField("Player name") { value = EditorGamePlatformState.PlayerName };
			playerName.RegisterValueChangedCallback(evt => EditorGamePlatformState.PlayerName = evt.newValue);
			foldout.Add(playerName);

			var activity = new Label();
			activity.style.whiteSpace = WhiteSpace.Normal;
			activity.style.fontSize = 10;
			activity.style.marginTop = 4;
			foldout.Add(activity);

			var reset = new Button(EditorGamePlatformState.Reset) { text = "Sign out and clear activity" };
			reset.style.marginTop = 2;
			foldout.Add(reset);

			void Refresh()
			{
				signedIn.SetValueWithoutNotify(EditorGamePlatformState.SignedIn);
				succeeds.SetValueWithoutNotify(EditorGamePlatformState.SignInSucceeds);
				playerName.SetValueWithoutNotify(EditorGamePlatformState.PlayerName);
				activity.text = EditorGamePlatformState.Activity.Count == 0
					? "No game-service activity yet."
					: "Recent activity:\n- " + string.Join("\n- ", EditorGamePlatformState.Activity);
			}

			foldout.RegisterCallback<AttachToPanelEvent>(_ =>
			{
				EditorGamePlatformState.Changed += Refresh;
				Refresh();
			});
			foldout.RegisterCallback<DetachFromPanelEvent>(_ => EditorGamePlatformState.Changed -= Refresh);
			Refresh();
			return foldout;
		}
	}
}
