using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameLovers.MobileServices.GamePlatform;
using GameLovers.MobileServices.GamePlatform.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class GamePlatformServicesPlayModeTest
	{
		private sealed class FakeBackend : IGamePlatformBackend
		{
			public readonly List<string> Calls = new List<string>();
			public readonly List<AwaitableCompletionSource<bool>> PendingAuthentications = new List<AwaitableCompletionSource<bool>>();
			public bool Authenticated;
			public bool AuthenticationResult = true;
			public int AuthenticateCalls;

			public bool IsAuthenticated => Authenticated;

			public GamePlayer LocalPlayer { get; set; } = new GamePlayer("p1", "Player One");

			public Awaitable<bool> AuthenticateAsync()
			{
				AuthenticateCalls++;
				var pending = new AwaitableCompletionSource<bool>();
				PendingAuthentications.Add(pending);
				return pending.Awaitable;
			}

			public void FinishAuthentication()
			{
				Authenticated = AuthenticationResult;
				foreach (var pending in PendingAuthentications) pending.SetResult(AuthenticationResult);
				PendingAuthentications.Clear();
			}

			public Awaitable<bool> ReportScoreAsync(string leaderboardId, long score) => Record($"score:{leaderboardId}:{score}");

			public Awaitable<bool> UnlockAchievementAsync(string achievementId) => Record($"unlock:{achievementId}");

			public Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps) =>
				Record($"increment:{achievementId}:{steps}:{totalSteps}");

			public void ShowLeaderboards(string leaderboardId) => Calls.Add($"leaderboards:{leaderboardId}");

			public void ShowAchievements() => Calls.Add("achievements");

			private Awaitable<bool> Record(string call)
			{
				Calls.Add(call);
				var completion = new AwaitableCompletionSource<bool>();
				completion.SetResult(true);
				return completion.Awaitable;
			}
		}

		private FakeBackend _backend;
		private GamePlatformServices _service;

		[SetUp]
		public void Init()
		{
			_backend = new FakeBackend();
			_service = new GamePlatformServices(_backend);
		}

		// Lets completed awaitables resume; asserting IsCompleted afterwards reports a stuck call instead of hanging the run.
		[UnityTest]
		// ADMIT: GamePlatformServices.AuthenticateAsync could start a second sign-in when the player is already signed in.
		// RCR: GamePlatformServices.cs AuthenticateAsync — already-signed-in shortcut disabled → RED (Expected: True But was: False). 2026-10-21
		public IEnumerator AuthenticateAsync_AlreadySignedIn_ReturnsTrueWithoutCallingThePlatform()
		{
			_backend.Authenticated = true;

			var operation = _service.AuthenticateAsync();
			yield return operation;

			Assert.IsTrue(operation.GetAwaiter().GetResult());
			Assert.AreEqual(0, _backend.AuthenticateCalls);
		}

		[UnityTest]
		// ADMIT: GamePlatformServices.AuthenticateAsync could open one sign-in sheet per concurrent caller.
		// RCR: GamePlatformServices.cs AuthenticateAsync — waiter branch disabled → RED (Expected: 1 But was: 2). 2026-10-21
		public IEnumerator AuthenticateAsync_ConcurrentCalls_ShareOnePlatformRequest()
		{
			var first = _service.AuthenticateAsync();
			var second = _service.AuthenticateAsync();
			_backend.FinishAuthentication();
			yield return Settle();

			Assert.IsTrue(first.GetAwaiter().IsCompleted && second.GetAwaiter().IsCompleted, "A concurrent sign-in call never completed.");
			Assert.AreEqual(1, _backend.AuthenticateCalls);
			Assert.IsTrue(first.GetAwaiter().GetResult());
			Assert.IsTrue(second.GetAwaiter().GetResult());
		}

		[UnityTest]
		// ADMIT: GamePlatformServices.AuthenticateAsync could keep a failed sign-in marked in flight, so a later retry never reaches the platform.
		// RCR: GamePlatformServices.cs AuthenticateAsync — waiter list not cleared → RED (The retry never completed. Expected: True But was: False). 2026-10-21
		public IEnumerator AuthenticateAsync_AfterFailure_AllowsARetry()
		{
			_backend.AuthenticationResult = false;
			var failed = _service.AuthenticateAsync();
			_backend.FinishAuthentication();
			yield return Settle();
			Assert.IsFalse(failed.GetAwaiter().GetResult());

			_backend.AuthenticationResult = true;
			var retry = _service.AuthenticateAsync();
			_backend.FinishAuthentication();
			yield return Settle();

			Assert.IsTrue(retry.GetAwaiter().IsCompleted, "The retry never completed.");
			Assert.AreEqual(2, _backend.AuthenticateCalls);
			Assert.IsTrue(retry.GetAwaiter().GetResult());
		}

		[UnityTest]
		// ADMIT: GamePlatformServices could send progress to the platform without a signed-in player, where the platform call fails opaquely.
		// RCR: GamePlatformServices.cs CanReport — signed-in guard disabled → RED (Expected log did not appear: [Warning] Regex: ReportScoreAsync\ needs\ a\ signed-in\ pl...). 2026-10-21
		public IEnumerator ReportingProgress_WhenSignedOut_ReturnsFalseWithoutCallingThePlatform()
		{
			LogAssert.Expect(LogType.Warning, Warning("ReportScoreAsync needs a signed-in player"));
			LogAssert.Expect(LogType.Warning, Warning("UnlockAchievementAsync needs a signed-in player"));
			LogAssert.Expect(LogType.Warning, Warning("IncrementAchievementAsync needs a signed-in player"));

			var score = _service.ReportScoreAsync("board", 10);
			var unlock = _service.UnlockAchievementAsync("ach");
			var increment = _service.IncrementAchievementAsync("ach", 1, 5);
			yield return score;
			yield return unlock;
			yield return increment;

			Assert.IsFalse(score.GetAwaiter().GetResult());
			Assert.IsFalse(unlock.GetAwaiter().GetResult());
			Assert.IsFalse(increment.GetAwaiter().GetResult());
			Assert.IsEmpty(_backend.Calls);
		}

		[UnityTest]
		// ADMIT: GamePlatformServices could forward an empty id to the platform instead of rejecting it.
		// RCR: GamePlatformServices.cs CanReport — empty-id guard disabled → RED (Expected log did not appear: [Warning] Regex: ReportScoreAsync\ needs\ a\ leaderboard\ id). 2026-10-21
		public IEnumerator ReportingProgress_WithoutAnId_ReturnsFalseWithoutCallingThePlatform()
		{
			_backend.Authenticated = true;
			LogAssert.Expect(LogType.Warning, Warning("ReportScoreAsync needs a leaderboard id"));
			LogAssert.Expect(LogType.Warning, Warning("UnlockAchievementAsync needs an achievement id"));

			var score = _service.ReportScoreAsync(string.Empty, 10);
			var unlock = _service.UnlockAchievementAsync(null);
			yield return score;
			yield return unlock;

			Assert.IsFalse(score.GetAwaiter().GetResult());
			Assert.IsFalse(unlock.GetAwaiter().GetResult());
			Assert.IsEmpty(_backend.Calls);
		}

		[UnityTest]
		// ADMIT: GamePlatformServices.IncrementAchievementAsync could forward zero or negative steps, which the platforms reject or treat as a reset.
		// RCR: GamePlatformServices.cs IncrementAchievementAsync — step guard disabled → RED (Expected log did not appear: [Warning] Regex: needs\ a\ positive\ step\ count,\ got\ 0). 2026-10-21
		public IEnumerator IncrementAchievementAsync_NonPositiveSteps_ReturnsFalseWithoutCallingThePlatform()
		{
			_backend.Authenticated = true;
			LogAssert.Expect(LogType.Warning, Warning("needs a positive step count, got 0"));

			var operation = _service.IncrementAchievementAsync("ach", 0, 5);
			yield return operation;

			Assert.IsFalse(operation.GetAwaiter().GetResult());
			Assert.IsEmpty(_backend.Calls);
		}

		[UnityTest]
		// ADMIT: GamePlatformServices could drop the score, ids or the iOS total step count on their way to the platform.
		// RCR: GamePlatformServices.cs IncrementAchievementAsync — totalSteps forced to 0 → RED (Expected is <System.String[3]>, actual is <System.Collections.Generic.List`1[System.Str...). 2026-10-21
		public IEnumerator SignedIn_ProgressCalls_ForwardEveryArgument()
		{
			_backend.Authenticated = true;

			var score = _service.ReportScoreAsync("board", 1234);
			var unlock = _service.UnlockAchievementAsync("first");
			var increment = _service.IncrementAchievementAsync("grind", 3, 10);
			yield return score;
			yield return unlock;
			yield return increment;

			CollectionAssert.AreEqual(new[] { "score:board:1234", "unlock:first", "increment:grind:3:10" }, _backend.Calls);
		}

		[Test]
		// ADMIT: GamePlatformServices.LocalPlayer could expose a stale player after the platform signed the player out.
		// RCR: GamePlatformServices.cs LocalPlayer — signed-in check removed → RED (Expected: null But was: <GameLovers.MobileServices.GamePlatform.GamePlayer>). 2026-10-21
		public void LocalPlayer_FollowsTheSignedInState()
		{
			Assert.IsNull(_service.LocalPlayer);

			_backend.Authenticated = true;

			Assert.AreEqual("Player One", _service.LocalPlayer.DisplayName);
		}

		[Test]
		// ADMIT: GamePlatformServices.ShowLeaderboards could open the platform UI for a signed-out player.
		// RCR: GamePlatformServices.cs ShowLeaderboards — signed-in guard disabled → RED (Expected: <empty> But was: < "leaderboards:board" >). 2026-10-21
		public void ShowUi_WhenSignedOut_DoesNotCallThePlatform()
		{
			LogAssert.Expect(LogType.Warning, Warning("ShowLeaderboards needs a signed-in player"));
			LogAssert.Expect(LogType.Warning, Warning("ShowAchievements needs a signed-in player"));

			_service.ShowLeaderboards("board");
			_service.ShowAchievements();

			Assert.IsEmpty(_backend.Calls);
		}

		[Test]
		// ADMIT: GamePlatformServices.ShowLeaderboards could lose the requested leaderboard id.
		// RCR: GamePlatformServices.cs ShowLeaderboards — leaderboard id replaced by null → RED (Expected is <System.String[3]>, actual is <System.Collections.Generic.List`1[System.Str...). 2026-10-21
		public void ShowUi_WhenSignedIn_ForwardsTheLeaderboardId()
		{
			_backend.Authenticated = true;

			_service.ShowLeaderboards("board");
			_service.ShowLeaderboards();
			_service.ShowAchievements();

			CollectionAssert.AreEqual(new[] { "leaderboards:board", "leaderboards:", "achievements" }, _backend.Calls);
		}

		private static Regex Warning(string text) => new Regex(Regex.Escape(text));

		private static IEnumerator Settle()
		{
			for (var i = 0; i < 3; i++) yield return null;
		}
	}
}
