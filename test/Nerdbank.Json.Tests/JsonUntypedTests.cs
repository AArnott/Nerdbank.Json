// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

public class JsonUntypedTests : TestBase
{
	[Test]
	[Arguments("null")]
	[Arguments("true")]
	[Arguments("false")]
	[Arguments("\"hello\"")]
	[Arguments("0")]
	[Arguments("-0")]
	[Arguments("1.0")]
	[Arguments("3.141592653589793")]
	[Arguments("1e10")]
	[Arguments("-1.5E+42")]
	[Arguments("123456789012345678901234567890")]
	[Arguments("[]")]
	[Arguments("{}")]
	[Arguments("[1,2,3]")]
	[Arguments("""{"a":1,"b":[true,null,"x"],"c":{"d":2}}""")]
	public void RoundTrip_PreservesJson(string json)
	{
		JsonValue? value = this.Serializer.DeserializeJsonValue(json);
		string reserialized = this.Serializer.SerializeJsonValue(value);
		Assert.Equal(json, reserialized);
	}

	[Test]
	public void String_Escapes_SemanticRoundTrip()
	{
		JsonValue? value = this.Serializer.DeserializeJsonValue("\"with \\\"escapes\\\" and \\u00e9\"");
		JsonString str = Assert.IsType<JsonString>(value);
		Assert.Equal("with \"escapes\" and \u00e9", str.Value);
		string reserialized = this.Serializer.SerializeJsonValue(value);
		Assert.Equal(value, this.Serializer.DeserializeJsonValue(reserialized));
	}

	[Test]
	public void Number_Fidelity_PreservesRawToken()
	{
		JsonValue? value = this.Serializer.DeserializeJsonValue("1.0");
		JsonNumber number = Assert.IsType<JsonNumber>(value);
		Assert.Equal("1.0", number.RawToken);
		Assert.Equal(1.0, number.GetDouble());

		JsonValue? big = this.Serializer.DeserializeJsonValue("123456789012345678901234567890");
		Assert.Equal("123456789012345678901234567890", ((JsonNumber)big!).RawToken);
	}

	[Test]
	public void Accessors_Work()
	{
		JsonValue? value = this.Serializer.DeserializeJsonValue("""{"n":42,"arr":[10,20],"s":"hi","b":true}""");
		JsonObject obj = Assert.IsType<JsonObject>(value);
		Assert.Equal(4, obj.Count);
		Assert.Equal(42, ((JsonNumber)obj["n"]).GetInt64());
		Assert.True(((JsonBoolean)obj["b"]).Value);
		Assert.Equal("hi", ((JsonString)obj["s"]).Value);
		JsonArray arr = (JsonArray)obj["arr"];
		Assert.Equal(2, arr.Count);
		Assert.Equal(20, ((JsonNumber)arr[1]).GetInt64());
		Assert.True(obj.TryGetValue("n", out _));
		Assert.False(obj.TryGetValue("missing", out _));
	}

	[Test]
	public void Equality_IsStructural()
	{
		JsonValue? a = this.Serializer.DeserializeJsonValue("""{"x":[1,2],"y":"z"}""");
		JsonValue? b = this.Serializer.DeserializeJsonValue("""{"y":"z","x":[1,2]}""");
		Assert.Equal(a, b);
		Assert.Equal(a!.GetHashCode(), b!.GetHashCode());

		JsonValue? c = this.Serializer.DeserializeJsonValue("""{"x":[1,3],"y":"z"}""");
		Assert.NotEqual(a, c);
	}

	[Test]
	public void DepthLimit_Enforced()
	{
		JsonSerializer serializer = new() { StartingContext = new SerializationContext { MaxDepth = 4 } };
		string json = "[[[[[1]]]]]";
		Assert.Throws<InvalidOperationException>(() => serializer.DeserializeJsonValue(json));
	}

	[Test]
	public void MemberCountLimit_Enforced()
	{
		JsonSerializer serializer = new()
		{
			StartingContext = new SerializationContext { Security = new SecuritySettings { MaxObjectMemberCount = 2 } },
		};
		Assert.Throws<FormatException>(() => serializer.DeserializeJsonValue("""{"a":1,"b":2,"c":3}"""));
	}

	[Test]
	public void DuplicateProperty_Rejected()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.DeserializeJsonValue("""{"a":1,"a":2}"""));
	}

	[Test]
	public void Comments_And_TrailingCommas()
	{
		JsonSerializer serializer = new()
		{
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
		};
		JsonValue? value = serializer.DeserializeJsonValue("""{/* c */"a":[1,2,],"b":3,}""");
		JsonObject obj = Assert.IsType<JsonObject>(value);
		Assert.Equal(2, ((JsonArray)obj["a"]).Count);
		Assert.Equal(3, ((JsonNumber)obj["b"]).GetInt64());
	}

	[Test]
	public void TrailingData_Throws()
	{
		Assert.Throws<FormatException>(() => this.Serializer.DeserializeJsonValue("[1,2] garbage"));
	}

	[Test]
	public async Task Async_RoundTrip_OverPipe()
	{
		JsonValue? value = this.Serializer.DeserializeJsonValue("""{"items":[1,2,3],"nested":{"x":true}}""");
		Pipe pipe = new();
		await this.Serializer.SerializeJsonValueAsync(pipe.Writer, value);
		await pipe.Writer.CompleteAsync();
		JsonValue? result = await this.Serializer.DeserializeJsonValueAsync(pipe.Reader);
		Assert.Equal(value, result);
	}

	[Test]
	public async Task Async_Deserialize_Chunked()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"a":[1,2,3],"b":"long string value here"}""");
		JsonValue? expected = this.Serializer.DeserializeJsonValue(utf8);
		using ChunkedStream stream = new(utf8, 1);
		JsonValue? result = await this.Serializer.DeserializeJsonValueAsync(stream);
		Assert.Equal(expected, result);
	}

	[Test]
	public void Create_Factories()
	{
		Assert.Equal(JsonValueKind.Boolean, JsonValue.Create(true).Kind);
		Assert.Same(JsonValue.Null, JsonValue.Create((string?)null));
		Assert.Equal("42", ((JsonNumber)JsonValue.Create(42L)).RawToken);
		Assert.Equal(JsonValueKind.Null, JsonValue.Null.Kind);
	}

	[Test]
	public void Stream_RoundTrip_Sync()
	{
		JsonValue? value = this.Serializer.DeserializeJsonValue("""[{"id":1},{"id":2}]""");
		using MemoryStream stream = new();
		this.Serializer.SerializeJsonValue(stream, value);
		stream.Position = 0;
		JsonValue? result = this.Serializer.DeserializeJsonValue(stream);
		Assert.Equal(value, result);
	}

	private sealed class ChunkedStream : Stream
	{
		private readonly byte[] data;
		private readonly int chunkSize;
		private int position;

		internal ChunkedStream(byte[] data, int chunkSize)
		{
			this.data = data;
			this.chunkSize = chunkSize;
		}

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => this.data.Length;

		public override long Position
		{
			get => this.position;
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int available = this.data.Length - this.position;
			if (available <= 0)
			{
				return 0;
			}

			int toCopy = Math.Min(Math.Min(count, this.chunkSize), available);
			Array.Copy(this.data, this.position, buffer, offset, toCopy);
			this.position += toCopy;
			return toCopy;
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
