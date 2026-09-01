using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Nerdbank.Json;
using PolyType;

// <AsyncStreaming>
[GenerateShape]
public partial record TelemetryBatch(string Source, Reading[] Readings);

[GenerateShape]
public partial record Reading(string Kind, double Value);

public static class AsyncStreamingSample
{
	public static async Task RoundTripOverStreamAsync(Stream stream)
	{
		JsonSerializer serializer = new();

		Reading[] readings = new Reading[10_000];
		for (int i = 0; i < readings.Length; i++)
		{
			readings[i] = new Reading("temp", i * 0.1);
		}

		TelemetryBatch batch = new("sensor-1", readings);

		// Writes incrementally to the stream; the stream is flushed but not closed.
		await serializer.SerializeAsync(stream, batch);

		stream.Position = 0;

		// Reads incrementally from the stream without buffering the whole document.
		TelemetryBatch? roundTripped = await serializer.DeserializeAsync<TelemetryBatch>(stream);
		System.Console.WriteLine(roundTripped!.Readings.Length);
	}

	public static async Task RoundTripOverPipeAsync()
	{
		JsonSerializer serializer = new();
		Pipe pipe = new();

		TelemetryBatch batch = new("sensor-2", [new Reading("humidity", 0.42)]);

		await serializer.SerializeAsync(pipe.Writer, batch);
		await pipe.Writer.CompleteAsync();

		TelemetryBatch? result = await serializer.DeserializeAsync<TelemetryBatch>(pipe.Reader);
		System.Console.WriteLine(result!.Source);
	}
}
// </AsyncStreaming>
