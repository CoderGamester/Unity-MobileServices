using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>One platform's game-service implementation: Game Center, Google Play Games, the Editor simulation or none.</summary>
	internal interface IGamePlatformBackend
	{
		bool IsAuthenticated { get; }
		GamePlayer LocalPlayer { get; }
		Awaitable<bool> AuthenticateAsync();
		Awaitable<bool> ReportScoreAsync(string leaderboardId, long score);
		Awaitable<bool> UnlockAchievementAsync(string achievementId);
		Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps);
		void ShowLeaderboards(string leaderboardId);
		void ShowAchievements();
	}
}
