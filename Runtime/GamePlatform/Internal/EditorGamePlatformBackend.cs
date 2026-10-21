#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>
	/// Process-wide Editor simulation of the game service, driven by the Device Simulator panel and read by every
	/// <see cref="GamePlatformServices"/> in the Editor.
	/// </summary>
	internal static class EditorGamePlatformState
	{
		private const int MaxActivity = 20;

		private static readonly List<string> _activity = new List<string>();

		/// <summary>Raised when the signed-in state or the activity log changes.</summary>
		public static event Action Changed;

		/// <summary>Whether a sign-in request succeeds, so the failure path can be exercised.</summary>
		public static bool SignInSucceeds = true;

		/// <summary>The name the simulated player has.</summary>
		public static string PlayerName = "Simulator Player";

		/// <summary>True while the simulated player is signed in.</summary>
		public static bool SignedIn { get; private set; }

		/// <summary>The most recent simulated game-service calls, newest first.</summary>
		public static IReadOnlyList<string> Activity => _activity;

		public static void SetSignedIn(bool signedIn)
		{
			SignedIn = signedIn;
			Record(signedIn ? "Signed in" : "Signed out");
		}

		public static void Record(string entry)
		{
			_activity.Insert(0, entry);
			if (_activity.Count > MaxActivity) _activity.RemoveAt(_activity.Count - 1);
			Changed?.Invoke();
		}

		public static void Reset()
		{
			SignedIn = false;
			SignInSucceeds = true;
			PlayerName = "Simulator Player";
			_activity.Clear();
			Changed?.Invoke();
		}
	}

	/// <summary>Editor backend that records what the game reports instead of calling a platform.</summary>
	internal sealed class EditorGamePlatformBackend : IGamePlatformBackend
	{
		public bool IsAuthenticated => EditorGamePlatformState.SignedIn;

		public GamePlayer LocalPlayer => IsAuthenticated ? new GamePlayer("simulator-player", EditorGamePlatformState.PlayerName) : null;

		public Awaitable<bool> AuthenticateAsync()
		{
			if (EditorGamePlatformState.SignInSucceeds)
			{
				EditorGamePlatformState.SetSignedIn(true);
			}
			else
			{
				EditorGamePlatformState.Record("Sign-in failed (simulated)");
			}
			return Completed(IsAuthenticated);
		}

		public Awaitable<bool> ReportScoreAsync(string leaderboardId, long score)
		{
			EditorGamePlatformState.Record($"Score {score} reported to '{leaderboardId}'");
			return Completed(true);
		}

		public Awaitable<bool> UnlockAchievementAsync(string achievementId)
		{
			EditorGamePlatformState.Record($"Achievement '{achievementId}' unlocked");
			return Completed(true);
		}

		public Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps)
		{
			EditorGamePlatformState.Record($"Achievement '{achievementId}' +{steps} steps (total {totalSteps})");
			return Completed(true);
		}

		public void ShowLeaderboards(string leaderboardId) =>
			EditorGamePlatformState.Record(string.IsNullOrEmpty(leaderboardId) ? "Showed all leaderboards" : $"Showed leaderboard '{leaderboardId}'");

		public void ShowAchievements() => EditorGamePlatformState.Record("Showed achievements");

		private static Awaitable<bool> Completed(bool result)
		{
			var completion = new AwaitableCompletionSource<bool>();
			completion.SetResult(result);
			return completion.Awaitable;
		}
	}
}
#endif
