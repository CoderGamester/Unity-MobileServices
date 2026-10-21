#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform.Internal
{
	/// <summary>
	/// Google Play Games Services v2 backend. It talks to the SDK through JNI and <c>OnCompleteListener</c> proxies, so
	/// the package compiles without the SDK; the build postprocessor adds it when a Play Games project id is configured.
	/// </summary>
	internal sealed class AndroidGamePlatformBackend : IGamePlatformBackend
	{
		private const string PlayGamesClass = "com.google.android.gms.games.PlayGames";
		private const int ShowUiRequestCode = 9004;

		private bool _authenticated;
		private GamePlayer _player;

		/// <inheritdoc />
		public bool IsAuthenticated => _authenticated;

		/// <inheritdoc />
		public GamePlayer LocalPlayer => _player;

		public AndroidGamePlatformBackend()
		{
			try
			{
				using var sdk = new AndroidJavaClass("com.google.android.gms.games.PlayGamesSdk");
				using var activity = CurrentActivity();
				sdk.CallStatic("initialize", activity);
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[GameLovers.MobileServices] Play Games Services is unavailable ({exception.Message}). " +
					"Enter the Play Games project id in the Mobile Services Config so the build includes the SDK.");
			}
		}

		/// <inheritdoc />
		public Awaitable<bool> AuthenticateAsync()
		{
			var completion = new AwaitableCompletionSource<bool>();
			try
			{
				var client = GetClient("getGamesSignInClient");
				AndroidTaskListeners.Listen(client.Call<AndroidJavaObject>("isAuthenticated"), checkTask =>
				{
					if (IsAuthenticatedResult(checkTask))
					{
						LoadPlayer(completion);
						return;
					}

					AndroidTaskListeners.Listen(client.Call<AndroidJavaObject>("signIn"), signInTask =>
					{
						if (!IsAuthenticatedResult(signInTask))
						{
							completion.TrySetResult(false);
							return;
						}

						LoadPlayer(completion);
					});
				});
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Play Games sign-in failed: {exception.Message}");
				completion.TrySetResult(false);
			}
			return NativeCompletion.ToMainThread(completion);
		}

		/// <inheritdoc />
		public Awaitable<bool> ReportScoreAsync(string leaderboardId, long score) =>
			Succeeded(() => GetClient("getLeaderboardsClient").Call<AndroidJavaObject>("submitScoreImmediate", leaderboardId, score));

		/// <inheritdoc />
		public Awaitable<bool> UnlockAchievementAsync(string achievementId) =>
			Succeeded(() => GetClient("getAchievementsClient").Call<AndroidJavaObject>("unlockImmediate", achievementId));

		/// <inheritdoc />
		public Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps) =>
			Succeeded(() => GetClient("getAchievementsClient").Call<AndroidJavaObject>("incrementImmediate", achievementId, steps));

		/// <inheritdoc />
		public void ShowLeaderboards(string leaderboardId)
		{
			var hasId = !string.IsNullOrEmpty(leaderboardId);
			ShowUi("getLeaderboardsClient", hasId ? "getLeaderboardIntent" : "getAllLeaderboardsIntent", hasId ? new object[] { leaderboardId } : Array.Empty<object>());
		}

		/// <inheritdoc />
		public void ShowAchievements() => ShowUi("getAchievementsClient", "getAchievementsIntent", Array.Empty<object>());

		private static AndroidJavaObject CurrentActivity()
		{
			using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		}

		private static AndroidJavaObject GetClient(string factoryMethod)
		{
			using var playGames = new AndroidJavaClass(PlayGamesClass);
			using var activity = CurrentActivity();
			return playGames.CallStatic<AndroidJavaObject>(factoryMethod, activity);
		}

		private static bool IsAuthenticatedResult(AndroidJavaObject task)
		{
			if (!task.Call<bool>("isSuccessful")) return false;
			using var result = task.Call<AndroidJavaObject>("getResult");
			return result != null && result.Call<bool>("isAuthenticated");
		}

		private void LoadPlayer(AwaitableCompletionSource<bool> completion)
		{
			AndroidTaskListeners.Listen(GetClient("getPlayersClient").Call<AndroidJavaObject>("getCurrentPlayer"), task =>
			{
				if (!task.Call<bool>("isSuccessful"))
				{
					_authenticated = false;
					_player = null;
					completion.TrySetResult(false);
					return;
				}

				using var player = task.Call<AndroidJavaObject>("getResult");
				_player = new GamePlayer(player.Call<string>("getPlayerId"), player.Call<string>("getDisplayName"));
				_authenticated = true;
				completion.TrySetResult(true);
			});
		}

		private Awaitable<bool> Succeeded(Func<AndroidJavaObject> startTask)
		{
			var completion = new AwaitableCompletionSource<bool>();
			try
			{
				AndroidTaskListeners.Listen(startTask(), task => completion.TrySetResult(task.Call<bool>("isSuccessful")));
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Play Games request failed: {exception.Message}");
				completion.TrySetResult(false);
			}
			return NativeCompletion.ToMainThread(completion);
		}

		private void ShowUi(string clientFactory, string intentMethod, object[] intentArguments)
		{
			try
			{
				AndroidTaskListeners.Listen(GetClient(clientFactory).Call<AndroidJavaObject>(intentMethod, intentArguments), task =>
				{
					if (!task.Call<bool>("isSuccessful")) return;
					using var intent = task.Call<AndroidJavaObject>("getResult");
					using var activity = CurrentActivity();
					activity.Call("startActivityForResult", intent, ShowUiRequestCode);
				});
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Play Games UI failed: {exception.Message}");
			}
		}
	}
}
#endif
