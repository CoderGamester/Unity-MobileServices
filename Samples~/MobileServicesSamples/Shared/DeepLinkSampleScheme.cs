using System;
using System.Text;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples
{
	/// <summary>Creates the deterministic URL scheme shared by the Deep Link sample UI and build hook.</summary>
	internal static class DeepLinkSampleScheme
	{
		private const string Fallback = "gamelovers-mobile-sample";

		internal static string FromIdentifier(string identifier)
		{
			if (string.IsNullOrWhiteSpace(identifier)) return Fallback;
			var builder = new StringBuilder(identifier.Length);
			var replacingInvalid = false;
			foreach (var character in identifier.Trim().ToLowerInvariant())
			{
				var valid = character >= 'a' && character <= 'z' ||
					character >= '0' && character <= '9' ||
					character == '+' || character == '-' || character == '.';
				if (valid)
				{
					builder.Append(character);
					replacingInvalid = false;
				}
				else if (!replacingInvalid)
				{
					builder.Append('-');
					replacingInvalid = true;
				}
			}

			var scheme = builder.ToString().Trim('-');
			if (string.IsNullOrEmpty(scheme)) return Fallback;
			return scheme[0] >= 'a' && scheme[0] <= 'z' ? scheme : "gl-" + scheme;
		}
	}
}
