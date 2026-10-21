#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>Game Center backend over <c>Plugins/iOS/GamePlatform.m</c>.</summary>
	internal sealed class IosGamePlatformBackend : IGamePlatformBackend
	{
		private delegate void GamePlatformCallback(int requestId, string result);

		private static readonly Dictionary<int, AwaitableCompletionSource<string>> _requests = new Dictionary<int, AwaitableCompletionSource<string>>();
		private static int _nextRequestId = 1;

		private GamePlayer _player;

		public bool IsAuthenticated => _GameLoversGamePlatformIsAuthenticated() != 0;

		public GamePlayer LocalPlayer => _player;

		public async Awaitable<bool> AuthenticateAsync()
		{
			var completion = new AwaitableCompletionSource<string>();
			_GameLoversGamePlatformAuthenticate(Register(completion), OnNativeResult);
			var payload = await NativeCompletion.ToMainThread(completion);
			_player = ParsePlayer(payload);
			return _player != null;
		}

		public async Awaitable<bool> ReportScoreAsync(string leaderboardId, long score)
		{
			var completion = new AwaitableCompletionSource<string>();
			_GameLoversGamePlatformReportScore(leaderboardId, score, Register(completion), OnNativeResult);
			return await NativeCompletion.ToMainThread(completion) != null;
		}

		public async Awaitable<bool> UnlockAchievementAsync(string achievementId)
		{
			var completion = new AwaitableCompletionSource<string>();
			_GameLoversGamePlatformUnlockAchievement(achievementId, Register(completion), OnNativeResult);
			return await NativeCompletion.ToMainThread(completion) != null;
		}

		public async Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps)
		{
			var completion = new AwaitableCompletionSource<string>();
			_GameLoversGamePlatformIncrementAchievement(achievementId, steps, totalSteps, Register(completion), OnNativeResult);
			return await NativeCompletion.ToMainThread(completion) != null;
		}

		public void ShowLeaderboards(string leaderboardId) => _GameLoversGamePlatformShowLeaderboards(leaderboardId);

		public void ShowAchievements() => _GameLoversGamePlatformShowAchievements();

		[DllImport("__Internal")] private static extern void _GameLoversGamePlatformAuthenticate(int requestId, GamePlatformCallback callback);
		[DllImport("__Internal")] private static extern int _GameLoversGamePlatformIsAuthenticated();
		[DllImport("__Internal")] private static extern void _GameLoversGamePlatformReportScore(string leaderboardId, long score, int requestId, GamePlatformCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversGamePlatformUnlockAchievement(string achievementId, int requestId, GamePlatformCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversGamePlatformIncrementAchievement(string achievementId, int steps, int totalSteps, int requestId, GamePlatformCallback callback);
		[DllImport("__Internal")] private static extern void _GameLoversGamePlatformShowLeaderboards(string leaderboardId);
		[DllImport("__Internal")] private static extern void _GameLoversGamePlatformShowAchievements();

		private static GamePlayer ParsePlayer(string payload)
		{
			if (string.IsNullOrEmpty(payload)) return null;
			var separator = payload.IndexOf('\n');
			return separator < 0
				? new GamePlayer(payload, string.Empty)
				: new GamePlayer(payload.Substring(0, separator), payload.Substring(separator + 1));
		}

		private static int Register(AwaitableCompletionSource<string> completion)
		{
			lock (_requests)
			{
				var id = _nextRequestId++;
				_requests[id] = completion;
				return id;
			}
		}

		[AOT.MonoPInvokeCallback(typeof(GamePlatformCallback))]
		private static void OnNativeResult(int requestId, string result)
		{
			AwaitableCompletionSource<string> completion;
			lock (_requests)
			{
				if (!_requests.TryGetValue(requestId, out completion)) return;
				_requests.Remove(requestId);
			}

			completion.TrySetResult(result);
		}
	}
}
#endif
