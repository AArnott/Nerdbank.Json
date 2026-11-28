// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Uncomment when adding a sample or intentionally changing its output, then run these tests once.
////#define RECORD

#if RECORD
#warning Recording mode is enabled. This should never be checked in.
#endif

using System.Reflection;

public class SampleOutputTests
{
	private const bool RecordMode =
#if RECORD
		true
#else
		false
#endif
		;

	private static readonly string OutputPath = typeof(SampleOutputTests).Assembly
		.GetCustomAttributes<AssemblyMetadataAttribute>()
		.Single(attribute => attribute.Key == "SampleOutputPath").Value!;

	private static readonly object RecordingLock = new();

	public static IEnumerable<(string Name, Func<string> Render)> Samples()
	{
		yield return ("Unions.LossyFarm", static () => Text(new JsonSerializer().Serialize(new Farm())));
		yield return ("Unions.RoundtrippingFarmAnimal", static () => Text(new JsonSerializer().Serialize(new UnionFarm())));
		yield return ("RuntimeUnions.RuntimeUnions", static () => Text(RuntimeUnionSamples.TaggedJson()));
		yield return ("RuntimeUnions.DuckTypingUnions", static () => Text(RuntimeUnionSamples.DuckJson()));
		yield return ("CSharpUnions.DocumentChange", static () => Text(DocumentChangeJson()));
		yield return ("CSharpUnions.Value.Auto", static () => Text(ValueJson(UnionTagging.Auto)));
		yield return ("CSharpUnions.Value.Tagged", static () => Text(ValueJson(UnionTagging.Tagged)));
	}

	[Test]
	[MethodDataSource(nameof(Samples))]
	public void OutputMatchesFile(string name, Func<string> render)
	{
		string path = Path.Combine(OutputPath, name + ".json");
		string actual = render().Replace("\r\n", "\n");
#pragma warning disable CS0162 // RecordMode is a compile-time constant.
		if (RecordMode)
		{
			lock (RecordingLock)
			{
				Directory.CreateDirectory(OutputPath);
				File.WriteAllText(path, actual);
			}

			return;
		}
#pragma warning restore CS0162

		string expected = File.ReadAllText(path).Replace("\r\n", "\n");
		Assert.Equal(expected, actual);
	}

	[Test]
	public void NoOrphanedOutputFiles()
	{
		HashSet<string> produced = new(Samples().Select(sample => sample.Name + ".json"), StringComparer.Ordinal);
#pragma warning disable CS0162 // RecordMode is a compile-time constant.
		if (RecordMode)
		{
			lock (RecordingLock)
			{
				if (!Directory.Exists(OutputPath))
				{
					return;
				}

				foreach (string file in Directory.GetFiles(OutputPath, "*.json"))
				{
					if (!produced.Contains(Path.GetFileName(file)))
					{
						File.Delete(file);
					}
				}
			}

			return;
		}
#pragma warning restore CS0162

		Assert.True(Directory.Exists(OutputPath), $"Sample output directory '{OutputPath}' does not exist.");
		string[] orphans = Directory.GetFiles(OutputPath, "*.json")
			.Select(Path.GetFileName)
			.Where(file => file is not null && !produced.Contains(file))
			.Cast<string>()
			.ToArray();
		Assert.Empty(orphans);
	}

	private static string DocumentChangeJson()
	{
		JsonSerializer serializer = new();
		string created = serializer.Serialize(new CSharpUnions.DocumentChange(new CSharpUnions.CreateFile("file:///a.cs")));
		string edited = serializer.Serialize(new CSharpUnions.DocumentChange(new CSharpUnions.TextDocumentEdit(new("file:///a.cs"), [])));
		return created + "\n" + edited;
	}

	private static string ValueJson(UnionTagging mode)
	{
		JsonSerializer serializer = new() { DefaultUnionTagging = mode };
		string number = serializer.Serialize(new CSharpUnions.SampleValue(5));
		string text = serializer.Serialize(new CSharpUnions.SampleValue("hi"));
		string point = serializer.Serialize(new CSharpUnions.SampleValue(new CSharpUnions.Point(1, 2)));
		return number + "\n" + text + "\n" + point;
	}

	private static string Text(string json) => json + "\n";
}
