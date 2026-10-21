#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
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

		private readonly List<AndroidJavaProxy> _listeners = new List<AndroidJavaProxy>();
		private bool _authenticated;
		private GamePlayer _player;

		public bool IsAuthenticated => _authenticated;

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

		public Awaitable<bool> AuthenticateAsync()
		{
			var completion = new AwaitableCompletionSource<bool>();
			try
			{
				var client = GetClient("getGamesSignInClient");
				Listen(client.Call<AndroidJavaObject>("isAuthenticated"), checkTask =>
				{
					if (IsAuthenticatedResult(checkTask))
					{
						LoadPlayer(completion);
						return;
					}

					Listen(client.Call<AndroidJavaObject>("signIn"), signInTask =>
					{
						if (IsAuthenticatedResult(signInTask)) LoadPlayer(completion);
						else Complete(completion, false);
					});
				});
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Play Games sign-in failed: {exception.Message}");
				Complete(completion, false);
			}
			return NativeCompletion.ToMainThread(completion);
		}

		public Awaitable<bool> ReportScoreAsync(string leaderboardId, long score) =>
			Succeeded(() => GetClient("getLeaderboardsClient").Call<AndroidJavaObject>("submitScoreImmediate", leaderboardId, score));

		public Awaitable<bool> UnlockAchievementAsync(string achievementId) =>
			Succeeded(() => GetClient("getAchievementsClient").Call<AndroidJavaObject>("unlockImmediate", achievementId));

		public Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps) =>
			Succeeded(() => GetClient("getAchievementsClient").Call<AndroidJavaObject>("incrementImmediate", achievementId, steps));

		public void ShowLeaderboards(string leaderboardId)
		{
			var hasId = !string.IsNullOrEmpty(leaderboardId);
			ShowUi("getLeaderboardsClient", hasId ? "getLeaderboardIntent" : "getAllLeaderboardsIntent", hasId ? new object[] { leaderboardId } : Array.Empty<object>());
		}

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

		private static void Complete(AwaitableCompletionSource<bool> completion, bool result) => completion.TrySetResult(result);

		private void LoadPlayer(AwaitableCompletionSource<bool> completion)
		{
			Listen(GetClient("getPlayersClient").Call<AndroidJavaObject>("getCurrentPlayer"), task =>
			{
				if (!task.Call<bool>("isSuccessful"))
				{
					_authenticated = false;
					_player = null;
					Complete(completion, false);
					return;
				}

				using var player = task.Call<AndroidJavaObject>("getResult");
				_player = new GamePlayer(player.Call<string>("getPlayerId"), player.Call<string>("getDisplayName"));
				_authenticated = true;
				Complete(completion, true);
			});
		}

		private Awaitable<bool> Succeeded(Func<AndroidJavaObject> startTask)
		{
			var completion = new AwaitableCompletionSource<bool>();
			try
			{
				Listen(startTask(), task => Complete(completion, task.Call<bool>("isSuccessful")));
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GameLovers.MobileServices] Play Games request failed: {exception.Message}");
				Complete(completion, false);
			}
			return NativeCompletion.ToMainThread(completion);
		}

		private void ShowUi(string clientFactory, string intentMethod, object[] intentArguments)
		{
			try
			{
				Listen(GetClient(clientFactory).Call<AndroidJavaObject>(intentMethod, intentArguments), task =>
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

		// Task callbacks arrive on Android's UI thread; the proxy is kept alive until it has run once.
		private void Listen(AndroidJavaObject task, Action<AndroidJavaObject> onComplete)
		{
			CompleteListener listener = null;
			listener = new CompleteListener(finished =>
			{
				lock (_listeners) _listeners.Remove(listener);
				try
				{
					onComplete(finished);
				}
				catch (Exception exception)
				{
					Debug.LogError($"[GameLovers.MobileServices] Play Games callback failed: {exception.Message}");
				}
			});
			lock (_listeners) _listeners.Add(listener);
			task.Call<AndroidJavaObject>("addOnCompleteListener", listener);
		}

		private sealed class CompleteListener : AndroidJavaProxy
		{
			private readonly Action<AndroidJavaObject> _onComplete;

			public CompleteListener(Action<AndroidJavaObject> onComplete) : base("com.google.android.gms.tasks.OnCompleteListener")
			{
				_onComplete = onComplete;
			}

			// ReSharper disable once InconsistentNaming
			public void onComplete(AndroidJavaObject task) => _onComplete(task);
		}
	}
}
#endif
