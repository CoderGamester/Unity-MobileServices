using System;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Asks the operating system for extra execution time when the application moves to the background, so a short
	/// task such as an upload, a save or a queue flush can finish.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Unity's main loop is paused while the application is in the background, so the protected work must run off
	/// the main thread (a thread-pool task, a native upload or file I/O). <c>Awaitable</c> and other main-thread
	/// continuations do not resume until the application returns to the foreground.
	/// </para>
	/// <para>
	/// iOS grants roughly 30 seconds. On Android the call is a pass-through: the process keeps running for a
	/// short grace period but the platform offers no per-task extension, so use a foreground service or
	/// WorkManager outside this package for long work.
	/// </para>
	/// </remarks>
	public interface IBackgroundTaskService
	{
		/// <summary>
		/// Begins a background task. Dispose the returned handle as soon as the work finishes; the system ends the
		/// task and may terminate the application if the handle is left open past the time limit.
		/// </summary>
		/// <param name="name">A short label for diagnostics.</param>
		/// <param name="onExpiring">
		/// Optional callback raised on the main thread when the system is about to suspend the application. The task
		/// ends right after it returns, so keep it to setting a cancellation flag.
		/// </param>
		BackgroundTaskHandle Begin(string name, Action onExpiring = null);
	}
}
