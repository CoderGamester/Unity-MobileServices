using System.Collections.Generic;
using GameLovers.MobileServices.GamePlatform.Internal;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform
{
	/// <inheritdoc />
	public sealed class GamePlatformServices : IGamePlatformServices
	{
		private readonly IGamePlatformBackend _backend;
		private List<AwaitableCompletionSource<bool>> _authenticationWaiters;

		/// <inheritdoc />
		public bool IsAuthenticated => _backend.IsAuthenticated;

		/// <inheritdoc />
		public GamePlayer LocalPlayer => IsAuthenticated ? _backend.LocalPlayer : null;

		public GamePlatformServices() : this(CreateBackend())
		{
		}

		internal GamePlatformServices(IGamePlatformBackend backend)
		{
			_backend = backend;
		}

		/// <inheritdoc />
		public async Awaitable<bool> AuthenticateAsync()
		{
			if (IsAuthenticated)
			{
				return true;
			}

			// Awaitables can only be awaited once, so callers that arrive mid sign-in wait on their own completion.
			if (_authenticationWaiters != null)
			{
				var waiter = new AwaitableCompletionSource<bool>();
				_authenticationWaiters.Add(waiter);
				return await waiter.Awaitable;
			}

			_authenticationWaiters = new List<AwaitableCompletionSource<bool>>();
			var success = false;
			try
			{
				success = await _backend.AuthenticateAsync();
			}
			finally
			{
				var waiters = _authenticationWaiters;
				_authenticationWaiters = null;
				foreach (var waiter in waiters) waiter.SetResult(success);
			}
			return success;
		}

		/// <inheritdoc />
		public Awaitable<bool> ReportScoreAsync(string leaderboardId, long score)
		{
			if (!CanReport(leaderboardId, "a leaderboard id", nameof(ReportScoreAsync)))
			{
				return Failed();
			}
			return _backend.ReportScoreAsync(leaderboardId, score);
		}

		/// <inheritdoc />
		public Awaitable<bool> UnlockAchievementAsync(string achievementId)
		{
			if (!CanReport(achievementId, "an achievement id", nameof(UnlockAchievementAsync)))
			{
				return Failed();
			}
			return _backend.UnlockAchievementAsync(achievementId);
		}

		/// <inheritdoc />
		public Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps = 0)
		{
			if (!CanReport(achievementId, "an achievement id", nameof(IncrementAchievementAsync)))
			{
				return Failed();
			}

			if (steps <= 0)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] {nameof(IncrementAchievementAsync)} needs a positive step count, got {steps}.");
				return Failed();
			}
			return _backend.IncrementAchievementAsync(achievementId, steps, totalSteps);
		}

		/// <inheritdoc />
		public void ShowLeaderboards(string leaderboardId = null)
		{
			if (!IsAuthenticated)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] {nameof(ShowLeaderboards)} needs a signed-in player; call {nameof(AuthenticateAsync)} first.");
				return;
			}
			_backend.ShowLeaderboards(leaderboardId);
		}

		/// <inheritdoc />
		public void ShowAchievements()
		{
			if (!IsAuthenticated)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] {nameof(ShowAchievements)} needs a signed-in player; call {nameof(AuthenticateAsync)} first.");
				return;
			}
			_backend.ShowAchievements();
		}

		private static IGamePlatformBackend CreateBackend()
		{
#if UNITY_EDITOR
			return new EditorGamePlatformBackend();
#elif UNITY_IOS
			return new IosGamePlatformBackend();
#elif UNITY_ANDROID
			return new AndroidGamePlatformBackend();
#else
			return new NoOpGamePlatformBackend();
#endif
		}

		private static Awaitable<bool> Failed()
		{
			var completion = new AwaitableCompletionSource<bool>();
			completion.SetResult(false);
			return completion.Awaitable;
		}

		private bool CanReport(string id, string idDescription, string operation)
		{
			if (string.IsNullOrEmpty(id))
			{
				Debug.LogWarning($"[GameLovers.MobileServices] {operation} needs {idDescription}.");
				return false;
			}

			if (!IsAuthenticated)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] {operation} needs a signed-in player; call {nameof(AuthenticateAsync)} first.");
				return false;
			}
			return true;
		}
	}
}
