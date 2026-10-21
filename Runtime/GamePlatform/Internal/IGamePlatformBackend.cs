using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>One platform's game service: Game Center, Google Play Games, the Editor simulation or none.</summary>
	internal interface IGamePlatformBackend
	{
		/// <summary>True while the platform reports a signed-in player.</summary>
		bool IsAuthenticated { get; }

		/// <summary>The signed-in player, or null.</summary>
		GamePlayer LocalPlayer { get; }

		/// <summary>Signs the player in, showing the platform's UI if needed; true when signed in afterwards.</summary>
		Awaitable<bool> AuthenticateAsync();

		/// <summary>Submits a score to a leaderboard; true when the platform accepted it.</summary>
		Awaitable<bool> ReportScoreAsync(string leaderboardId, long score);

		/// <summary>Unlocks an achievement; true when the platform accepted it.</summary>
		Awaitable<bool> UnlockAchievementAsync(string achievementId);

		/// <summary>Adds steps to an incremental achievement; true when the platform accepted it.</summary>
		Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps);

		/// <summary>Opens the leaderboard UI for one board, or for all boards when the id is null or empty.</summary>
		void ShowLeaderboards(string leaderboardId);

		/// <summary>Opens the achievements UI.</summary>
		void ShowAchievements();
	}
}
