// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using PolyType;

[GenerateShapeFor<CustomReferenceConverterTests.Node>]
public partial class CustomReferenceConverterTests
{
	[Test]
	public void CustomConverterCanReportCycle()
	{
		JsonSerializer serializer = CreateSerializer();
		Node original = new();
		original.Next = original;
		string json = serializer.Serialize<Node, CustomReferenceConverterTests>(original);
		Assert.Equal("""{"$id":1,"$value":{"next":{"$ref":1}}}""", json);
		Node result = serializer.Deserialize<Node, CustomReferenceConverterTests>(json)!;
		Assert.Same(result, result.Next);
	}

	[Test]
	public async Task AsyncCustomConverterCanReportCycle()
	{
		JsonSerializer serializer = CreateSerializer();
		Node original = new();
		original.Next = original;
		using MemoryStream stream = new();
		ITypeShape<Node> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Node>()!;
		await serializer.SerializeAsync(stream, original, shape);
		stream.Position = 0;
		Node result = (await serializer.DeserializeAsync(stream, shape))!;
		Assert.Same(result, result.Next);
	}

	[Test]
	public async Task PublicUntypedConverterWrappers()
	{
		JsonConverter converter = new NodeConverter();
		SerializationContext context = new();
		Pipe buffer = new();
		JsonWriter writer = new(buffer.Writer);
		converter.WriteObject(ref writer, null, context);
		writer.Flush();
		buffer.Writer.Complete();
		JsonReader reader = new(Encoding.UTF8.GetBytes("null"));
		Assert.Null(converter.ReadObject(ref reader, context));
		buffer.Reader.Complete();
		Assert.Throws<InvalidCastException>(() => WriteWrongType(converter));

		using MemoryStream stream = new();
		PipeWriter pipeWriter = PipeWriter.Create(stream, new(leaveOpen: true));
		await converter.WriteObjectAsync(new JsonAsyncWriter(pipeWriter), null, context);
		await pipeWriter.FlushAsync();
		await pipeWriter.CompleteAsync();
		Assert.Equal("null", Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		PipeReader pipeReader = PipeReader.Create(stream, new(leaveOpen: true));
		using JsonAsyncReader asyncReader = new(pipeReader) { CancellationToken = default };
		Assert.Null(await converter.ReadObjectAsync(asyncReader, context));
		await pipeReader.CompleteAsync();
	}

	private static JsonSerializer CreateSerializer() => new()
	{
		Converters = [new NodeConverter()],
		PreserveReferences = ReferencePreservationMode.AllowCycles,
	};

	private static void WriteWrongType(JsonConverter converter)
	{
		Pipe buffer = new();
		try
		{
			JsonWriter writer = new(buffer.Writer);
			converter.WriteObject(ref writer, "wrong", new());
		}
		finally
		{
			buffer.Writer.Complete();
			buffer.Reader.Complete();
		}
	}

	internal sealed class Node
	{
		public Node? Next { get; set; }
	}

	private sealed class NodeConverter : JsonConverter<Node>
	{
		public override void Write(ref JsonWriter writer, in Node? value, SerializationContext context)
		{
			if (value is null)
			{
				writer.WriteNullValue();
				return;
			}

			context.DepthStep();
			writer.WriteStartObject();
			writer.WritePropertyName("next");
			context.GetConverter<Node, CustomReferenceConverterTests>().Write(ref writer, value.Next, context);
			writer.WriteEndObject();
		}

		public override Node? Read(ref JsonReader reader, SerializationContext context)
		{
			if (reader.TryReadNull())
			{
				return null;
			}

			context.DepthStep();
			Node result = new();
			context.ReportObjectConstructed(result);
			reader.ReadStartObject();
			Assert.Equal("next", reader.ReadRequiredString());
			reader.ReadNameSeparator();
			if (!reader.TryReadNull())
			{
				result.Next = context.GetConverter<Node, CustomReferenceConverterTests>().Read(ref reader, context);
			}

			Assert.True(reader.TryReadEndObject());
			return result;
		}
	}
}
