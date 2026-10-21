using System.Linq;
using System.Text.RegularExpressions;
using GameLovers.MobileServices.Device;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	[TestFixture]
	public class AppShortcutRulesTest
	{
		private static AppShortcut Shortcut(string id) => new AppShortcut(id, "Title " + id, "mygame://" + id);

		[Test]
		// ADMIT: AppShortcutRules.Normalize could reorder valid shortcuts, so the OS menu does not match the order the game set.
		// RCR: AppShortcutRules.cs Normalize — shortcuts inserted at the front → RED (Expected and actual are both <System.String[3]> Values differ at index [0] String lengt...). 2026-10-21
		public void Normalize_ValidShortcuts_KeepTheirOrder()
		{
			var result = AppShortcutRules.Normalize(new[] { Shortcut("b"), Shortcut("a"), Shortcut("c") });

			CollectionAssert.AreEqual(new[] { "b", "a", "c" }, result.Select(shortcut => shortcut.Id).ToArray());
		}

		[Test]
		// ADMIT: AppShortcutRules.Normalize could pass a shortcut with no id, title or absolute URI to the OS, which rejects or crashes on it.
		// RCR: AppShortcutRules.cs Normalize — absolute-URI check removed → RED (Expected is <System.String[1]>, actual is <System.String[2]> Values differ at index [0]...). 2026-10-21
		public void Normalize_IncompleteShortcuts_AreSkipped()
		{
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut ''"));
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut 'no-title'"));
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut 'relative'"));
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut ''"));

			var result = AppShortcutRules.Normalize(new[]
			{
				new AppShortcut(string.Empty, "Title", "mygame://x"),
				new AppShortcut("no-title", " ", "mygame://x"),
				new AppShortcut("relative", "Title", "daily/reward"),
				null,
				Shortcut("ok"),
			});

			CollectionAssert.AreEqual(new[] { "ok" }, result.Select(shortcut => shortcut.Id).ToArray());
		}

		[Test]
		// ADMIT: AppShortcutRules.Normalize could keep a repeated id, which the OS collapses unpredictably.
		// RCR: AppShortcutRules.cs Normalize — repeated-id check disabled → RED (Expected: 1 But was: 2). 2026-10-21
		public void Normalize_RepeatedId_KeepsTheFirst()
		{
			LogAssert.Expect(LogType.Warning, new Regex("the id is already used"));

			var result = AppShortcutRules.Normalize(new[] { new AppShortcut("a", "First", "mygame://1"), new AppShortcut("a", "Second", "mygame://2") });

			Assert.AreEqual(1, result.Count);
			Assert.AreEqual("First", result[0].Title);
		}

		[Test]
		// ADMIT: AppShortcutRules.Normalize could pass more shortcuts than the platforms show, so the extras are dropped by the OS in an unknown order.
		// RCR: AppShortcutRules.cs Normalize — limit check disabled → RED (Expected is <System.String[4]>, actual is <System.String[5]> Values differ at index [4]...). 2026-10-21
		public void Normalize_MoreThanTheLimit_KeepsTheFirstOnes()
		{
			LogAssert.Expect(LogType.Warning, new Regex("Skipped shortcut 'e'"));

			var result = AppShortcutRules.Normalize(new[] { Shortcut("a"), Shortcut("b"), Shortcut("c"), Shortcut("d"), Shortcut("e") });

			CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, result.Select(shortcut => shortcut.Id).ToArray());
		}

		[Test]
		// ADMIT: AppShortcutRules.Normalize could throw on a null list instead of clearing to nothing.
		// RCR: AppShortcutRules.cs Normalize — null guard removed → RED (System.NullReferenceException : Object reference not set to an instance of an object). 2026-10-21
		public void Normalize_Null_ReturnsEmpty()
		{
			Assert.AreEqual(0, AppShortcutRules.Normalize(null).Count);
		}
	}
}
