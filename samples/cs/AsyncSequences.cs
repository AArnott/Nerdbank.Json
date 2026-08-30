using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Json;
using PolyType;

// <AsyncSequences>
[GenerateShape]
public partial record LogEntry(long Timestamp, string Message);

[GenerateShape]
public partial record LogFile(string Source, LogEntry[] Entries);

public static class AsyncSequenceSample
{
	// Stream the elements of a large top-level JSON array without buffering the whole document.
	public static async Task ProcessArrayAsync(Stream stream, JsonSerializer serializer)
	{
		await foreach (LogEntry? entry in serializer.DeserializeArrayAsync<LogEntry>(stream))
		{
			System.Console.WriteLine(entry!.Message);
		}
	}

	// Stream newline-delimited JSON (JSON Lines): one value per line.
	public static async Task ProcessNdjsonAsync(PipeReader reader, JsonSerializer serializer, CancellationToken cancellationToken)
	{
		await foreach (LogEntry? entry in serializer
			.DeserializeNewlineDelimitedAsync<LogEntry>(reader)
			.WithCancellation(cancellationToken))
		{
			System.Console.WriteLine(entry!.Timestamp);
		}
	}

	// Stream an array selected by a typed path inside an object envelope. The preamble ("source") and the
	// trailing envelope are consumed incrementally; only one entry is materialized at a time.
	public static async Task ProcessEnvelopeAsync(Stream stream, JsonSerializer serializer)
	{
		await foreach (LogEntry? entry in serializer.DeserializeArrayAtAsync<LogFile, LogEntry>(stream, f => f.Entries))
		{
			System.Console.WriteLine(entry!.Message);
		}
	}

	// Serialize an asynchronous source as a top-level JSON array with backpressure.
	public static async Task WriteArrayAsync(Stream stream, JsonSerializer serializer)
	{
		await serializer.SerializeArrayAsync<LogEntry>(stream, ProduceAsync());

		static async IAsyncEnumerable<LogEntry> ProduceAsync()
		{
			for (int i = 0; i < 100; i++)
			{
				await Task.Yield();
				yield return new LogEntry(i, $"entry {i}");
			}
		}
	}
}
// </AsyncSequences>
