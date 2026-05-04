using System;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Wraps <c>Application.deepLinkActivated</c> with cold-start link queueing — links the OS handed
	/// the app at launch are not lost if the first <see cref="OnLinkActivated"/> subscriber attaches
	/// after the event has already fired.
	/// </summary>
	public interface IDeepLinkService
	{
		/// <summary>
		/// Fires whenever the OS delivers a deep link. If the app was cold-launched with a link and
		/// no subscriber was attached yet, the link is replayed to the first subscriber.
		/// </summary>
		event Action<Uri> OnLinkActivated;

		/// <summary>
		/// The cold-start link, if any. Reads as <c>null</c> after the first <see cref="OnLinkActivated"/>
		/// subscriber consumes it (or after the first delivered runtime link, whichever comes first).
		/// </summary>
		Uri PendingColdStartLink { get; }
	}
}
