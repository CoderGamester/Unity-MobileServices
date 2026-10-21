// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>
	/// One home-screen quick action: the long-press menu entry on the app icon. Tapping it opens the app with
	/// <see cref="Uri"/> delivered through <see cref="IDeepLinkService"/>.
	/// </summary>
	public sealed class AppShortcut
	{
		/// <summary>A stable id, unique among the shortcuts the game sets.</summary>
		public string Id { get; }

		/// <summary>The label shown in the menu.</summary>
		public string Title { get; }

		/// <summary>The absolute deep-link URI opened on tap, for example <c>mygame://daily</c>.</summary>
		public string Uri { get; }

		/// <summary>An optional second line: the subtitle on iOS, the long label on Android.</summary>
		public string Subtitle { get; }

		public AppShortcut(string id, string title, string uri, string subtitle = null)
		{
			Id = id;
			Title = title;
			Uri = uri;
			Subtitle = subtitle;
		}
	}
}
