using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>Backend for platforms without a game service: every request fails.</summary>
	internal sealed class NoOpGamePlatformBackend : IGamePlatformBackend
	{
		/// <inheritdoc />
		public bool IsAuthenticated => false;

		/// <inheritdoc />
		public GamePlayer LocalPlayer => null;

		/// <inheritdoc />
		public Awaitable<bool> AuthenticateAsync() => Failed();

		/// <inheritdoc />
		public Awaitable<bool> ReportScoreAsync(string leaderboardId, long score) => Failed();

		/// <inheritdoc />
		public Awaitable<bool> UnlockAchievementAsync(string achievementId) => Failed();

		/// <inheritdoc />
		public Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps) => Failed();

		/// <inheritdoc />
		public void ShowLeaderboards(string leaderboardId)
		{
		}

		/// <inheritdoc />
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
