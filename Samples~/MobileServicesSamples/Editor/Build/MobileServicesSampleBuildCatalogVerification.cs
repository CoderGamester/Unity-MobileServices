using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples.Editor.Build
{
	/// <summary>Writes an identity artifact for the imported, serialized sample scene catalog.</summary>
	public static class MobileServicesSampleBuildCatalogVerification
	{
		private const string OutputEnvironmentVariable = "MOBILE_SAMPLES_CATALOG_ARTIFACT";

		[MenuItem("Tools/Mobile Samples Examples/Verify Scene Catalog", priority = 102)]
		private static void VerifyFromMenu() => WriteArtifact();

		/// <summary>Writes page, current asset path, and derived GUID rows for shell verification.</summary>
		public static void WriteArtifact()
		{
			var output = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
			if (string.IsNullOrWhiteSpace(output))
			{
				throw new InvalidOperationException($"Set {OutputEnvironmentVariable} to a writable artifact path before invoking catalog verification.");
			}

			if (!MobileServicesSampleBuildCatalog.TryGetOrderedScenePaths(out var paths, out var error))
			{
				throw new InvalidOperationException(error);
			}

			var lines = new List<string> { "mobile-services-sample-catalog-v1" };
			for (var i = 0; i < paths.Length; i++)
			{
				var page = MobileServicesSamplePages.All[i];
				var guid = AssetDatabase.AssetPathToGUID(paths[i]);
				if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException($"The {MobileServicesSamplePages.GetDisplayName(page)} scene path '{paths[i]}' has no derived GUID.");
				lines.Add($"{page}\t{paths[i]}\t{guid}");
			}

			Directory.CreateDirectory(Path.GetDirectoryName(output));
			File.WriteAllLines(output, lines);
			Debug.Log($"[GameLovers.MobileServices.Samples] Scene catalog artifact: entries={paths.Length} path='{output}'");
		}
	}
}
