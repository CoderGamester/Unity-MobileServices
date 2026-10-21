using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices
{
	/// <summary>Hands the result of a native callback back to Unity's main thread.</summary>
	internal static class NativeCompletion
	{
		/// <summary>
		/// Awaits <paramref name="completion"/>, which a native callback may complete on any thread, and resumes on the
		/// main thread so consumer code never runs on an OS callback thread.
		/// </summary>
		public static async Awaitable<T> ToMainThread<T>(AwaitableCompletionSource<T> completion)
		{
			var result = await completion.Awaitable;
			await Awaitable.MainThreadAsync();
			return result;
		}
	}
}
