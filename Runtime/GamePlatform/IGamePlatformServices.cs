using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.GamePlatform
{
	/// <summary>The signed-in player of the platform's game service (Game Center or Google Play Games).</summary>
	public sealed class GamePlayer
	{
		/// <summary>The platform's stable identifier for the player in this game.</summary>
		public string Id { get; }

		/// <summary>The name the player chose on the platform.</summary>
		public string DisplayName { get; }

		public GamePlayer(string id, string displayName)
		{
			Id = id;
			DisplayName = displayName;
		}
	}

	/// <summary>
	/// A leaderboard or achievement id that differs per platform. Game Center and Google Play Games each issue their
	/// own ids, so keep both in one value and pass it anywhere an id string is expected.
	/// </summary>
	public readonly struct PlatformId
	{
		/// <summary>The Game Center id.</summary>
		public string Ios { get; }

		/// <summary>The Google Play Games id.</summary>
		public string Android { get; }

		public PlatformId(string ios, string android)
		{
			Ios = ios;
			Android = android;
		}

		/// <summary>The id for the running platform; outside iOS and Android it falls back to whichever id is set.</summary>
		public static implicit operator string(PlatformId id) => id.Resolve(Application.platform);

		internal string Resolve(RuntimePlatform platform)
		{
			switch (platform)
			{
				case RuntimePlatform.IPhonePlayer: return Ios;
				case RuntimePlatform.Android: return Android;
				default: return string.IsNullOrEmpty(Ios) ? Android : Ios;
			}
		}
	}

	/// <summary>
	/// Sign-in, leaderboards and achievements on the platform's game service: Game Center on iOS and Google Play
	/// Games on Android. It is opt-in: turn on the Game Center capability and enter the Play Games project id in the
	/// Mobile Services Config.
	/// </summary>
	/// <remarks>
	/// Every call that reports progress needs a signed-in player and returns <c>false</c>, after a log message, when
	/// there is none. Call <see cref="AuthenticateAsync"/> first.
	/// </remarks>
	public interface IGamePlatformServices
	{
		/// <summary>True while a player is signed in to the platform's game service.</summary>
		bool IsAuthenticated { get; }

		/// <summary>The signed-in player, or null when nobody is signed in.</summary>
		GamePlayer LocalPlayer { get; }

		/// <summary>Slot-based cloud saves on Game Center saved games (iOS) or Google Play Games saved games (Android).</summary>
		ICloudSaveService CloudSave { get; }

		/// <summary>
		/// Signs the player in, showing the platform's sign-in UI when needed. Returns immediately when already signed in.
		/// </summary>
		/// <returns>True when a player is signed in afterwards.</returns>
		Awaitable<bool> AuthenticateAsync();

		/// <summary>Submits a score to a leaderboard. The platform keeps the best score according to the board's rules.</summary>
		/// <param name="leaderboardId">The leaderboard id; pass a <see cref="PlatformId"/> to supply one per platform.</param>
		/// <param name="score">The score value.</param>
		/// <returns>True when the platform accepted the score.</returns>
		Awaitable<bool> ReportScoreAsync(string leaderboardId, long score);

		/// <summary>Unlocks an achievement.</summary>
		/// <param name="achievementId">The achievement id; pass a <see cref="PlatformId"/> to supply one per platform.</param>
		/// <returns>True when the platform accepted the unlock.</returns>
		Awaitable<bool> UnlockAchievementAsync(string achievementId);

		/// <summary>Adds progress to an incremental achievement.</summary>
		/// <param name="achievementId">The achievement id; pass a <see cref="PlatformId"/> to supply one per platform.</param>
		/// <param name="steps">The number of steps to add; must be positive.</param>
		/// <param name="totalSteps">
		/// The steps needed to complete the achievement. Required on iOS, where Game Center tracks a percentage;
		/// Android reads the total from the Play Console and ignores it.
		/// </param>
		/// <returns>True when the platform accepted the progress.</returns>
		Awaitable<bool> IncrementAchievementAsync(string achievementId, int steps, int totalSteps = 0);

		/// <summary>Opens the platform's leaderboard UI, for one board or for all boards when no id is given.</summary>
		void ShowLeaderboards(string leaderboardId = null);

		/// <summary>Opens the platform's achievements UI.</summary>
		void ShowAchievements();
	}
}
