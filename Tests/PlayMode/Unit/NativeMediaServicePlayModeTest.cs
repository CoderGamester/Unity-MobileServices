using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using GameLovers.MobileServices.NativeUi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace
namespace GameLoversEditor.MobileServices.Tests
{
	public class NativeMediaServicePlayModeTest
	{
		private Func<string, MediaType, bool> _saveOverride;
		private Func<string> _pickOverride;
		private string _file;

		[SetUp]
		public void Init()
		{
			_saveOverride = NativeMediaService.EditorSaveToGalleryOverride;
			_pickOverride = NativeMediaService.EditorPickImageOverride;
			NativeMediaService.EditorSaveToGalleryOverride = null;
			NativeMediaService.EditorPickImageOverride = null;
			_file = Path.Combine(Application.temporaryCachePath, "native-media-test.png");
			File.WriteAllBytes(_file, new byte[] { 1, 2, 3 });
		}

		[TearDown]
		public void Cleanup()
		{
			NativeMediaService.EditorSaveToGalleryOverride = _saveOverride;
			NativeMediaService.EditorPickImageOverride = _pickOverride;
			if (File.Exists(_file)) File.Delete(_file);
		}

		[UnityTest]
		// ADMIT: NativeMediaService.SaveToGalleryAsync could hand a missing file to the platform bridge instead of failing fast.
		// RCR: NativeMediaService.cs SaveToGalleryAsync — missing-file guard disabled → RED (Expected log did not appear: [Warning] Regex: SaveToGallery needs an existing file). 2026-10-21
		public IEnumerator SaveToGalleryAsync_MissingFile_ReturnsFalseWithoutCallingThePlatform()
		{
			var called = false;
			NativeMediaService.EditorSaveToGalleryOverride = (_, _) => called = true;
			LogAssert.Expect(LogType.Warning, new Regex("SaveToGallery needs an existing file"));

			var operation = NativeMediaService.SaveToGalleryAsync(Path.Combine(Application.temporaryCachePath, "missing.png"));
			yield return operation;

			Assert.IsFalse(operation.GetAwaiter().GetResult());
			Assert.IsFalse(called);
		}

		[UnityTest]
		// ADMIT: NativeMediaService.SaveToGalleryAsync could drop the media type, saving a video as an image.
		// RCR: NativeMediaService.cs SaveToGalleryAsync — media type forced to Image → RED (Expected: Video But was: Image). 2026-10-21
		public IEnumerator SaveToGalleryAsync_ExistingFile_ForwardsPathAndMediaType()
		{
			string savedPath = null;
			var savedType = MediaType.Image;
			NativeMediaService.EditorSaveToGalleryOverride = (path, type) =>
			{
				savedPath = path;
				savedType = type;
				return true;
			};

			var operation = NativeMediaService.SaveToGalleryAsync(_file, MediaType.Video);
			yield return operation;

			Assert.IsTrue(operation.GetAwaiter().GetResult());
			Assert.AreEqual(_file, savedPath);
			Assert.AreEqual(MediaType.Video, savedType);
		}

		[UnityTest]
		// ADMIT: NativeMediaService.SaveToGalleryAsync could report success when the platform failed to save.
		// RCR: NativeMediaService.cs SaveToGalleryAsync — failure mapped to success → RED (Expected: False But was: True). 2026-10-21
		public IEnumerator SaveToGalleryAsync_PlatformFails_ReturnsFalse()
		{
			NativeMediaService.EditorSaveToGalleryOverride = (_, _) => false;

			var operation = NativeMediaService.SaveToGalleryAsync(_file);
			yield return operation;

			Assert.IsFalse(operation.GetAwaiter().GetResult());
		}

		[UnityTest]
		// ADMIT: NativeMediaService.PickImageAsync could return a placeholder path when the player cancels.
		// RCR: NativeMediaService.cs PickImageAsync — cancel replaced by a placeholder path → RED (Expected: null But was: "none"). 2026-10-21
		public IEnumerator PickImageAsync_PlayerCancels_ReturnsNull()
		{
			NativeMediaService.EditorPickImageOverride = () => null;

			var operation = NativeMediaService.PickImageAsync();
			yield return operation;

			Assert.IsNull(operation.GetAwaiter().GetResult());
		}

		[UnityTest]
		// ADMIT: NativeMediaService.PickImageAsync could drop the picked path instead of returning it to the caller.
		// RCR: NativeMediaService.cs PickImageAsync — picked path dropped → RED (Expected: "/var/folders/rp/gfglg7lj7s70g8vjjw1y9b6r0000gr/T/DefaultCompany/Scratch/nati...). 2026-10-21
		public IEnumerator PickImageAsync_PlayerPicks_ReturnsThePickedPath()
		{
			NativeMediaService.EditorPickImageOverride = () => _file;

			var operation = NativeMediaService.PickImageAsync();
			yield return operation;

			Assert.AreEqual(_file, operation.GetAwaiter().GetResult());
		}
	}
}
