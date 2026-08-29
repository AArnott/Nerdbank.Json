// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may contain closely related model types.
#pragma warning disable SA1500 // Multidimensional array literals use a compact nested-brace layout.

using System.Collections.Generic;
using PolyType.Abstractions;

internal static class ManyModels
{
	internal static void Run()
	{
		JsonSerializer serializer = new();
		DeviceSnapshot snapshot = CreateSnapshot();
		string json = serializer.Serialize(snapshot);

		DeviceSnapshot roundTripped = serializer.Deserialize<DeviceSnapshot>(json)!;
		Verify(snapshot, roundTripped);

		VerifyCycle();
		VerifyRuntimeUnion();
		VerifySchema();

		Console.WriteLine("Success");
	}

	private static void VerifySchema()
	{
		JsonSerializer serializer = new();
		string schema = serializer.GetJsonSchema<DeviceSnapshot>();

		if (!schema.Contains("\"$schema\"") || !schema.Contains("\"type\":\"object\"") || !schema.Contains("\"deviceId\""))
		{
			throw new InvalidOperationException("JSON schema export did not describe the model.");
		}
	}

	private static void VerifyRuntimeUnion()
	{
		JsonSerializer serializer = new()
		{
			Unions = JsonUnionConfiguration.Default.WithUnion(
				JsonUnion<Payload>.Create()
					.AddCase<TextPayload>("text", ShapeOf<TextPayload>())
					.AddCase<NumberPayload>("number", ShapeOf<NumberPayload>())),
		};

		Payload value = new TextPayload("hello");
		string json = serializer.Serialize<Payload>(value);
		Payload roundTripped = serializer.Deserialize<Payload>(json)!;

		if (roundTripped is not TextPayload { Text: "hello" })
		{
			throw new InvalidOperationException("Runtime union configuration did not round-trip.");
		}
	}

	private static ITypeShape<T> ShapeOf<T>()
		where T : IShapeable<T> => T.GetTypeShape();

	private static void VerifyCycle()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.AllowCycles };
		LinkNode node = new() { Label = "self" };
		node.Next = node;

		string json = serializer.Serialize(node);
		LinkNode roundTripped = serializer.Deserialize<LinkNode>(json)!;

		if (!ReferenceEquals(roundTripped, roundTripped.Next) || roundTripped.Label != "self")
		{
			throw new InvalidOperationException("Reference cycle did not round-trip under AllowCycles.");
		}
	}

	private static DeviceSnapshot CreateSnapshot()
		=> new()
		{
			DeviceId = Guid.Parse("74f0c65d-4f23-4d24-8d42-8c76ccad88a8"),
			Name = "Weather station",
			Firmware = new Version(2, 1, 0, 7),
			Endpoint = new Uri("https://example.com/api/devices/74f0c65d-4f23-4d24-8d42-8c76ccad88a8"),
			Culture = CultureInfo.InvariantCulture,
			Encoding = Encoding.UTF8,
			Timestamp = new DateTimeOffset(2026, 6, 26, 12, 30, 45, TimeSpan.Zero),
			Uptime = TimeSpan.FromHours(36),
			Accent = Color.CornflowerBlue,
			Location = new Point(12, -34),
			Payload = [1, 2, 3, 4],
			Tags = new Dictionary<int, string>
			{
				[1] = "roof",
				[2] = "outdoor",
			},
			Labels = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["Region"] = "north",
			},
			Readings =
			[
				new SensorReading { Name = "temperature", Value = 21.5m },
				new SensorReading { Name = "humidity", Value = 0.42m },
			],
			Grid = new int[,] { { 1, 2, 3 }, { 4, 5, 6 } },
		};

	private static void Verify(DeviceSnapshot expected, DeviceSnapshot actual)
	{
		if (expected.DeviceId != actual.DeviceId ||
			expected.Name != actual.Name ||
			expected.Firmware != actual.Firmware ||
			expected.Endpoint != actual.Endpoint ||
			expected.Culture.Name != actual.Culture.Name ||
			expected.Encoding.WebName != actual.Encoding.WebName ||
			expected.Timestamp != actual.Timestamp ||
			expected.Uptime != actual.Uptime ||
			expected.Accent.ToArgb() != actual.Accent.ToArgb() ||
			expected.Location != actual.Location)
		{
			throw new InvalidOperationException("Scalar round-trip mismatch.");
		}

		if (!expected.Payload.AsSpan().SequenceEqual(actual.Payload))
		{
			throw new InvalidOperationException("Payload round-trip mismatch.");
		}

		if (expected.Tags.Count != actual.Tags.Count || expected.Readings.Count != actual.Readings.Count || !actual.CallbackObserved)
		{
			throw new InvalidOperationException("Collection counts changed during round-trip or the deserialization callback did not run.");
		}

		if (actual.Labels.Count != 1 || !actual.Labels.TryGetValue("REGION", out string? region) || region != "north")
		{
			throw new InvalidOperationException("Member-specified collection comparer was not applied during deserialization.");
		}

		if (actual.Grid.Rank != 2 || actual.Grid.GetLength(0) != 2 || actual.Grid.GetLength(1) != 3 || actual.Grid[1, 2] != 6)
		{
			throw new InvalidOperationException("Rectangular multidimensional array did not round-trip.");
		}

		for (int i = 0; i < expected.Readings.Count; i++)
		{
			if (expected.Readings[i].Name != actual.Readings[i].Name || expected.Readings[i].Value != actual.Readings[i].Value)
			{
				throw new InvalidOperationException("Reading round-trip mismatch.");
			}
		}
	}
}

[GenerateShape]
internal partial class DeviceSnapshot : IJsonSerializationCallbacks
{
	private bool callbackObserved;

	public Guid DeviceId { get; set; }

	public string Name { get; set; } = string.Empty;

	public Version Firmware { get; set; } = new(1, 0);

	public Uri Endpoint { get; set; } = new("https://example.com", UriKind.Absolute);

	public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

	public Encoding Encoding { get; set; } = Encoding.UTF8;

	public DateTimeOffset Timestamp { get; set; }

	public TimeSpan Uptime { get; set; }

	public Color Accent { get; set; }

	public Point Location { get; set; }

	public byte[] Payload { get; set; } = [];

	public Dictionary<int, string> Tags { get; set; } = [];

	[JsonCollectionComparer(typeof(CaseInsensitiveStringComparer))]
	public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

	public List<SensorReading> Readings { get; set; } = [];

	public int[,] Grid { get; set; } = new int[0, 0];

	internal bool CallbackObserved => this.callbackObserved;

	public void OnBeforeSerialize()
	{
	}

	public void OnAfterDeserialize() => this.callbackObserved = true;
}

internal sealed class CaseInsensitiveStringComparer : IEqualityComparer<string>
{
	public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);

	public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
}

[GenerateShape]
internal partial class SensorReading
{
	public string Name { get; set; } = string.Empty;

	public decimal Value { get; set; }
}

[GenerateShape]
internal partial class LinkNode
{
	public string? Label { get; set; }

	public LinkNode? Next { get; set; }
}

[GenerateShape]
internal abstract partial record Payload;

[GenerateShape]
internal partial record TextPayload(string Text) : Payload;

[GenerateShape]
internal partial record NumberPayload(int Value) : Payload;
