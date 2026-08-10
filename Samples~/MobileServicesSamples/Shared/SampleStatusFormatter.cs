using System;
using System.Text;

// ReSharper disable once CheckNamespace
namespace GameLovers.MobileServices.Samples
{
	/// <summary>Formats sample status cards as one sentence-case field per line.</summary>
	internal static class SampleStatusFormatter
	{
		internal static string Format(params SampleStatusEntry[] entries)
		{
			if (entries == null || entries.Length == 0) return string.Empty;
			var builder = new StringBuilder();
			foreach (var entry in entries)
			{
				if (string.IsNullOrWhiteSpace(entry.Label)) continue;
				if (builder.Length > 0) builder.AppendLine();
				builder.Append(entry.Label.Trim()).Append(": ").Append(entry.Value);
			}
			return builder.ToString();
		}

		internal static string YesNo(bool value) => value ? "Yes" : "No";
	}

	/// <summary>Stores one normalized label and value for a sample status card.</summary>
	internal readonly struct SampleStatusEntry
	{
		internal string Label { get; }

		internal string Value { get; }

		internal SampleStatusEntry(string label, object value)
		{
			Label = label;
			var text = value?.ToString();
			Value = string.IsNullOrWhiteSpace(text) ? "None" : text;
		}
	}
}
