using System;
using System.Collections;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Haptics.Internal
{
	/// <summary>
	/// Internal MonoBehaviour that owns the single auto-stop coroutine for time-bounded haptics.
	/// Spawned lazily by <see cref="HapticsService"/> the first time a <c>Play*</c> call is made.
	/// </summary>
	internal sealed class HapticsHost : MonoBehaviour
	{
		private Coroutine _activeCoroutine;
		private Action _onStop;

		/// <summary>Schedules a one-time stop after <paramref name="delaySeconds"/> real-time seconds.</summary>
		public void ScheduleStop(float delaySeconds, Action onStop)
		{
			Cancel();
			_onStop = onStop;
			_activeCoroutine = StartCoroutine(StopAfterDelay(delaySeconds));
		}

		/// <summary>Cancels any pending auto-stop. Does not invoke the stop callback.</summary>
		public void Cancel()
		{
			if (_activeCoroutine != null)
			{
				StopCoroutine(_activeCoroutine);
				_activeCoroutine = null;
			}
			_onStop = null;
		}

		private IEnumerator StopAfterDelay(float delaySeconds)
		{
			yield return new WaitForSecondsRealtime(delaySeconds);
			var callback = _onStop;
			_onStop = null;
			_activeCoroutine = null;
			callback?.Invoke();
		}

		private void OnDestroy()
		{
			Cancel();
		}
	}
}
