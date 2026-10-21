using System;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// A running background task. Its members may be read from any thread, so the protected work can poll
	/// <see cref="IsExpired"/> between steps.
	/// </summary>
	public sealed class BackgroundTaskHandle : IDisposable
	{
		private readonly Action _end;
		private readonly Func<TimeSpan> _remainingTime;
		private readonly Func<bool> _isExpired;
		private volatile bool _disposed;

		/// <summary>The label passed to <see cref="IBackgroundTaskService.Begin"/>.</summary>
		public string Name { get; }

		/// <summary>True when the system has run out of time for this task and is about to suspend the application.</summary>
		public bool IsExpired => !_disposed && _isExpired();

		/// <summary>True until the task is disposed or expires.</summary>
		public bool IsActive => !_disposed && !_isExpired();

		/// <summary>
		/// Time left before the system suspends the application, or <see cref="TimeSpan.MaxValue"/> when the
		/// application is in the foreground or the platform sets no limit.
		/// </summary>
		public TimeSpan RemainingTime => _disposed ? TimeSpan.Zero : _remainingTime();

		internal BackgroundTaskHandle(string name, Action end, Func<TimeSpan> remainingTime, Func<bool> isExpired)
		{
			Name = name;
			_end = end;
			_remainingTime = remainingTime;
			_isExpired = isExpired;
		}

		/// <summary>Ends the task. Calling it again has no effect.</summary>
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_end();
		}
	}
}
