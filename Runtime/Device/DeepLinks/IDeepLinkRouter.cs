using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Routing layer over <see cref="IDeepLinkService"/>. Maps URI path patterns to handlers and
	/// dispatches each incoming deep link to the first matching route.
	/// </summary>
	/// <remarks>
	/// <para>Path-pattern syntax is intentionally minimal — the goal is to remove the per-consumer
	/// switch-on-segments boilerplate, not to grow into a full URL-routing DSL:</para>
	/// <list type="bullet">
	/// <item>Literal segments match exactly. <c>/settings</c> matches <c>myapp://settings</c>.</item>
	/// <item>Segments prefixed with <c>:</c> capture into the params dict. <c>/promo/:id</c> matches
	/// <c>myapp://promo/abc123</c> yielding <c>{ "id": "abc123" }</c>.</item>
	/// <item>Routes are checked in registration order; the first match wins.</item>
	/// </list>
	/// <para>The router subscribes once to <see cref="IDeepLinkService.OnLinkActivated"/> at
	/// construction; consumers should hold the router instance for the lifetime of the app to avoid
	/// re-subscription churn.</para>
	/// </remarks>
	public interface IDeepLinkRouter
	{
		/// <summary>
		/// Registers a route. Path-pattern syntax: literal segments match exactly, segments prefixed
		/// with <c>:</c> capture into the handler's <c>params</c> argument.
		/// </summary>
		void MapRoute(string pathPattern, Action<Uri, IReadOnlyDictionary<string, string>> handler);

		/// <summary>Removes the route previously registered with <paramref name="pathPattern"/>. No-op if absent.</summary>
		void RemoveRoute(string pathPattern);

		/// <summary>
		/// Attempts to dispatch <paramref name="uri"/> through the registered routes. Returns
		/// <c>true</c> when a route matched and its handler was invoked.
		/// </summary>
		bool TryDispatch(Uri uri);
	}
}
