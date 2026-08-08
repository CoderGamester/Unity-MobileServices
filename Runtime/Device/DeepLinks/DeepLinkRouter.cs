using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// Dispatches deep-link paths to handlers registered through <see cref="IDeepLinkRouter"/>.
	/// </summary>
	/// <remarks>
	/// The constructor configures all routes before it subscribes, so a cold-start replay sees the
	/// complete route table and a configuration failure leaves no handler on the caller-owned service.
	/// </remarks>
	public sealed class DeepLinkRouter : IDeepLinkRouter, IDisposable
	{
		private readonly IDeepLinkService _deepLink;
		private readonly List<Route> _routes = new List<Route>();
		private bool _disposed;

		public DeepLinkRouter(IDeepLinkService deepLink, Action<IDeepLinkRouter> configure)
		{
			_deepLink = deepLink ?? throw new ArgumentNullException(nameof(deepLink));
			if (configure == null) throw new ArgumentNullException(nameof(configure));

			// Configure before subscribing so a thrown callback leaves no handler on the caller-owned service.
			configure(this);
			_deepLink.OnLinkActivated += OnLinkActivated;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_deepLink.OnLinkActivated -= OnLinkActivated;
			_routes.Clear();
		}

		/// <inheritdoc />
		public void MapRoute(string pathPattern, Action<Uri, IReadOnlyDictionary<string, string>> handler)
		{
			ThrowIfDisposed();
			if (string.IsNullOrEmpty(pathPattern)) throw new ArgumentNullException(nameof(pathPattern));
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			_routes.Add(new Route(pathPattern, handler));
		}

		/// <inheritdoc />
		public void RemoveRoute(string pathPattern)
		{
			ThrowIfDisposed();
			if (string.IsNullOrEmpty(pathPattern)) return;
			for (var i = _routes.Count - 1; i >= 0; i--)
			{
				if (_routes[i].Pattern == pathPattern)
				{
					_routes.RemoveAt(i);
				}
			}
		}

		/// <inheritdoc />
		public bool TryDispatch(Uri uri)
		{
			ThrowIfDisposed();
			if (uri == null) return false;
			foreach (var route in _routes)
			{
				if (route.TryMatch(uri, out var captured))
				{
					route.Handler(uri, captured);
					return true;
				}
			}
			return false;
		}

		private void OnLinkActivated(Uri uri)
		{
			if (_disposed)
			{
				return;
			}

			TryDispatch(uri);
		}

		private void ThrowIfDisposed()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(DeepLinkRouter));
			}
		}

		private sealed class Route
		{
			public readonly string Pattern;
			public readonly Action<Uri, IReadOnlyDictionary<string, string>> Handler;
			private readonly string[] _segments;

			public Route(string pattern, Action<Uri, IReadOnlyDictionary<string, string>> handler)
			{
				Pattern = pattern;
				Handler = handler;
				_segments = SplitPath(pattern);
			}

			public bool TryMatch(Uri uri, out IReadOnlyDictionary<string, string> captured)
			{
				// Build the URI path segments. Treat host as the first segment so myapp://promo/123
				// matches /promo/:id (most app schemes carry the "type" in host, the "id" in path).
				var segments = SplitUri(uri);

				if (segments.Length != _segments.Length)
				{
					captured = null;
					return false;
				}

				var dict = new Dictionary<string, string>();
				for (var i = 0; i < _segments.Length; i++)
				{
					var pat = _segments[i];
					if (pat.StartsWith(":", StringComparison.Ordinal))
					{
						dict[pat.Substring(1)] = segments[i];
					}
					else if (!string.Equals(pat, segments[i], StringComparison.OrdinalIgnoreCase))
					{
						captured = null;
						return false;
					}
				}
				captured = dict;
				return true;
			}

			private static string[] SplitPath(string pattern)
			{
				return pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
			}

			private static string[] SplitUri(Uri uri)
			{
				var host = uri.Host ?? string.Empty;
				var path = uri.AbsolutePath?.Trim('/') ?? string.Empty;
				var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

				if (string.IsNullOrEmpty(host))
				{
					return parts;
				}

				var combined = new string[parts.Length + 1];
				combined[0] = host;
				for (var i = 0; i < parts.Length; i++)
				{
					combined[i + 1] = parts[i];
				}
				return combined;
			}
		}
	}
}
