using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>Backend for platforms without a game service: every request fails.</summary>
	internal sealed class NoOpGamePlatformBackend : IGamePlatformBackend
	{
		public bool IsAuthenticated => false;

		public GamePlayer LocalPlayer => null;

		public Awaitable<bool> AuthenticateAsync() => Failed();

		public Awaitable<bool> ReportScoreAsync(string leaderboardId, long score) => Failed();

		public Awaitable<bool> UnlockAchievementAsync(string achievementId) => Failed();

		public Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps) => Failed();

		public void ShowLeaderboards(string leaderboardId)
		{
		}

		public void ShowAchievements()
		{
		}

		private static Awaitable<bool> Failed()
		{
			Debug.LogWarning("[GameLovers.MobileServices] Game services are only available on iOS and Android.");
			var completion = new AwaitableCompletionSource<bool>();
			completion.SetResult(false);
			return completion.Awaitable;
		}
	}
}
