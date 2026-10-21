using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Device
{
	/// <summary>Validation shared by both platforms before shortcuts reach the OS.</summary>
	internal static class AppShortcutRules
	{
		/// <summary>The most dynamic shortcuts both platforms reliably show.</summary>
		public const int MaxShortcuts = 4;

		/// <summary>
		/// Returns the usable shortcuts in order: entries with no id, title or absolute URI are dropped, the first of a
		/// repeated id wins, and the list is cut to <see cref="MaxShortcuts"/>. Each removal is logged.
		/// </summary>
		public static IReadOnlyList<AppShortcut> Normalize(IReadOnlyList<AppShortcut> shortcuts)
		{
			var result = new List<AppShortcut>();
			if (shortcuts == null)
			{
				return result;
			}

			var ids = new HashSet<string>();
			foreach (var shortcut in shortcuts)
			{
				if (shortcut == null || string.IsNullOrWhiteSpace(shortcut.Id) || string.IsNullOrWhiteSpace(shortcut.Title) ||
					!Uri.TryCreate(shortcut.Uri, UriKind.Absolute, out _))
				{
					Debug.LogWarning($"[GameLovers.MobileServices] Skipped shortcut '{shortcut?.Id}': it needs an id, a title and an absolute URI.");
					continue;
				}

				if (!ids.Add(shortcut.Id))
				{
					Debug.LogWarning($"[GameLovers.MobileServices] Skipped shortcut '{shortcut.Id}': the id is already used.");
					continue;
				}

				if (result.Count == MaxShortcuts)
				{
					Debug.LogWarning($"[GameLovers.MobileServices] Skipped shortcut '{shortcut.Id}': at most {MaxShortcuts} shortcuts are supported.");
					continue;
				}

				result.Add(shortcut);
			}
			return result;
		}
	}
}
