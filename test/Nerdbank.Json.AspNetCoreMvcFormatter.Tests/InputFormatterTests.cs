// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Nerdbank.Json.AspNetCoreMvcFormatter.Tests;

public class InputFormatterTests : FormatterTestBase
{
	private readonly JsonInputFormatter formatter = new(Provider);

	[Test]
	public void Constructors_ValidateArguments()
	{
		Assert.Throws<ArgumentNullException>(() => new JsonInputFormatter(null!));
		Assert.Throws<ArgumentNullException>(() => new JsonInputFormatter(null!, new JsonSerializer()));
		Assert.Throws<ArgumentNullException>(() => new JsonInputFormatter(Provider, null!));
	}

	[Test]
	public async Task ReadAsync_DeserializesModel()
	{
		byte[] body = Encoding.UTF8.GetBytes("""{"name":"Ada","age":36}""");
		InputFormatterContext context = CreateInputContext(new MemoryStream(body), "application/json", typeof(Person));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.False(result.HasError);
		Person person = Assert.IsType<Person>(result.Model);
		Assert.Equal("Ada", person.Name);
		Assert.Equal(36, person.Age);
	}

	[Test]
	public async Task ReadAsync_Utf16Charset()
	{
		byte[] body = Encoding.Unicode.GetBytes("""{"name":"Zoë","age":7}""");
		InputFormatterContext context = CreateInputContext(new MemoryStream(body), "application/json; charset=utf-16", typeof(Person));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Person person = Assert.IsType<Person>(result.Model);
		Assert.Equal("Zoë", person.Name);
	}

	[Test]
	public async Task ReadAsync_MalformedJson_AddsModelError()
	{
		byte[] body = Encoding.UTF8.GetBytes("""{"name":"Ada","age":}""");
		InputFormatterContext context = CreateInputContext(new MemoryStream(body), "application/json", typeof(Person));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.True(result.HasError);
		Assert.False(context.ModelState.IsValid);
		Assert.True(context.ModelState.ContainsKey("model"));
	}

	[Test]
	[Arguments(false, false)]
	[Arguments(false, true)]
	[Arguments(true, false)]
	[Arguments(true, true)]
	public async Task ReadAsync_TrailingData_AddsModelError(bool utf16, bool chunked)
	{
		string[] suffixes = ["{}", " null", " 42", " garbage", "]", "\uFEFF", "\uFEFF{}", " /* comment */", " /* unterminated"];
		foreach (string suffix in suffixes)
		{
			Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
			byte[] body = encoding.GetBytes("""{"name":"Ada","age":36}""" + suffix);
			using Stream stream = chunked ? new OneByteAtATimeStream(body) : new MemoryStream(body);
			InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person));
			context.ModelState.AddModelError("existing", "Keep this error.");

			InputFormatterResult result = await this.formatter.ReadAsync(context);

			Assert.True(result.HasError, $"Accepted trailing data: {suffix}");
			Assert.False(result.IsModelSet);
			Assert.True(context.ModelState.ContainsKey("model"));
			Assert.Equal("Keep this error.", Assert.Single(context.ModelState["existing"]!.Errors).ErrorMessage);
			Assert.True(stream.CanRead);
		}
	}

	[Test]
	[Arguments(false, false)]
	[Arguments(false, true)]
	[Arguments(true, false)]
	[Arguments(true, true)]
	public async Task ReadAsync_TrailingWhitespaceAndComments(bool utf16, bool chunked)
	{
		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		string[] suffixes = [" \t\r\n", " /* comment */ \r\n // final comment", " // comment\r\n /* final comment */"];
		foreach (string suffix in suffixes)
		{
			byte[] body = encoding.GetBytes("""{"name":"Ada","age":36}""" + suffix);
			using Stream stream = chunked ? new OneByteAtATimeStream(body) : new MemoryStream(body);
			InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person));
			JsonInputFormatter commentsFormatter = new(Provider, new JsonSerializer { ReadCommentHandling = JsonCommentHandling.Skip });

			InputFormatterResult result = await commentsFormatter.ReadAsync(context);

			Assert.False(result.HasError);
			Assert.Equal(new Person("Ada", 36), Assert.IsType<Person>(result.Model));
			Assert.True(stream.CanRead);
		}
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_CommentsEnabled_RejectsInvalidTrailingData(bool utf16)
	{
		string[] suffixes = [" /* comment */ {}", " // comment\n null", " /* comment */ \uFEFF"];
		foreach (string suffix in suffixes)
		{
			Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
			using OneByteAtATimeStream stream = new(encoding.GetBytes("""{"name":"Ada","age":36}""" + suffix));
			InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person));
			JsonInputFormatter commentsFormatter = new(Provider, new JsonSerializer { ReadCommentHandling = JsonCommentHandling.Skip });

			InputFormatterResult result = await commentsFormatter.ReadAsync(context);

			Assert.True(result.HasError, $"Accepted trailing data: {suffix}");
			Assert.True(context.ModelState.ContainsKey("model"));
		}
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_CommentsEnabled_UnterminatedTrailingComment_AddsModelError(bool utf16)
	{
		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		using OneByteAtATimeStream stream = new(encoding.GetBytes("""{"name":"Ada","age":36} /* unterminated"""));
		InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person));
		JsonInputFormatter commentsFormatter = new(Provider, new JsonSerializer { ReadCommentHandling = JsonCommentHandling.Skip });

		InputFormatterResult result = await commentsFormatter.ReadAsync(context);

		Assert.True(result.HasError);
		Assert.True(context.ModelState.ContainsKey("model"));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_InvalidTrailingEncoding_AddsModelError(bool utf16)
	{
		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		byte[] body = [.. encoding.GetBytes("""{"name":"Ada","age":36}"""), 0xFF];
		using OneByteAtATimeStream stream = new(body);
		InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.True(result.HasError);
		Assert.True(context.ModelState.ContainsKey("model"));
		Assert.True(stream.CanRead);
	}

	[Test]
	public async Task ReadAsync_PreservesPeekedBodyReaderBytes()
	{
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""{"name":"Ada","age":36}""" + " \r\n"));
		InputFormatterContext context = CreateInputContext(stream, "application/json", typeof(Person));
		PipeReader reader = context.HttpContext.Request.BodyReader;
		ReadResult buffered = await reader.ReadAsync();
		Assert.Equal(stream.Length, stream.Position);
		reader.AdvanceTo(buffered.Buffer.Start, buffered.Buffer.Start);

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.False(result.HasError);
		Assert.Equal(new Person("Ada", 36), Assert.IsType<Person>(result.Model));
		Assert.True(stream.CanRead);
		ReadResult remaining = await reader.ReadAsync();
		Assert.True(remaining.IsCompleted);
		Assert.True(remaining.Buffer.IsEmpty);
		reader.AdvanceTo(remaining.Buffer.End);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_InitialBom_IsAllowed(bool utf16)
	{
		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		using OneByteAtATimeStream stream = new(encoding.GetBytes("\uFEFF" + """{"name":"Ada","age":36}"""));
		InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.False(result.HasError);
		Assert.Equal(new Person("Ada", 36), Assert.IsType<Person>(result.Model));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_Null_SetsModel(bool utf16)
	{
		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		using MemoryStream stream = new(encoding.GetBytes("null \r\n"));
		InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.False(result.HasError);
		Assert.True(result.IsModelSet);
		Assert.Null(result.Model);
		Assert.True(stream.CanRead);
	}

	[Test]
	public async Task ReadAsync_EmptyBody_NoValue()
	{
		InputFormatterContext context = CreateInputContext(new MemoryStream([]), "application/json", typeof(Person));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.False(result.HasError);
		Assert.False(result.IsModelSet);
	}

	[Test]
	public async Task ReadAsync_EmptyBody_TreatAsDefault()
	{
		InputFormatterContext context = CreateInputContext(new MemoryStream([]), "application/json", typeof(Person), treatEmptyInputAsDefaultValue: true);

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.False(result.HasError);
		Assert.True(result.IsModelSet);
		Assert.Null(result.Model);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_EmptyBody_ValueTypeDefault(bool utf16)
	{
		using MemoryStream stream = new([]);
		InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(int), treatEmptyInputAsDefaultValue: true);

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Assert.False(result.HasError);
		Assert.True(result.IsModelSet);
		Assert.Equal(0, Assert.IsType<int>(result.Model));
		Assert.True(stream.CanRead);
	}

	[Test]
	public async Task ReadAsync_ProviderMiss_Throws()
	{
		byte[] body = Encoding.UTF8.GetBytes("""{"x":1}""");
		InputFormatterContext context = CreateInputContext(new MemoryStream(body), "application/json", typeof(Unregistered));

		await Assert.ThrowsAsync<MissingTypeShapeException>(async () => await this.formatter.ReadAsync(context));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_Cancellation_Throws(bool utf16)
	{
		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		byte[] body = encoding.GetBytes("""{"name":"Ada","age":36}""");
		using CancellationTokenSource cts = new();
		cts.Cancel();
		using MemoryStream stream = new(body);
		InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person), requestAborted: cts.Token);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await this.formatter.ReadAsync(context));
		Assert.True(context.ModelState.IsValid);
		Assert.True(stream.CanRead);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReadAsync_CancellationWhileValidatingTrailingBytes_Throws(bool utf16)
	{
		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		using CancellationTokenSource cts = new();
		using CancelAtEndStream stream = new(encoding.GetBytes("""{"name":"Ada","age":36}""" + " \r\n"), cts);
		InputFormatterContext context = CreateInputContext(stream, utf16 ? "application/json; charset=utf-16" : "application/json", typeof(Person), requestAborted: cts.Token);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await this.formatter.ReadAsync(context));

		Assert.True(context.ModelState.IsValid);
		Assert.True(stream.CanRead);
	}

	[Test]
	public async Task ReadAsync_ChunkedLargeBody()
	{
		int[] numbers = new int[2000];
		for (int i = 0; i < numbers.Length; i++)
		{
			numbers[i] = i;
		}

		string serialized = new JsonSerializer().Serialize(new Bag(numbers));
		byte[] body = Encoding.UTF8.GetBytes(serialized);
		using OneByteAtATimeStream stream = new(body);
		InputFormatterContext context = CreateInputContext(stream, "application/json", typeof(Bag));

		InputFormatterResult result = await this.formatter.ReadAsync(context);

		Bag bag = Assert.IsType<Bag>(result.Model);
		Assert.Equal(2000, bag.Numbers.Length);
		Assert.Equal(1999, bag.Numbers[1999]);
	}

	[Test]
	[Arguments("application/json", true)]
	[Arguments("text/json", true)]
	[Arguments("application/vnd.custom+json", true)]
	[Arguments("text/plain", false)]
	public void CanRead_MediaTypes(string contentType, bool expected)
	{
		InputFormatterContext context = CreateInputContext(new MemoryStream([]), contentType, typeof(Person));
		Assert.Equal(expected, this.formatter.CanRead(context));
	}

	private sealed class CancelAtEndStream(byte[] data, CancellationTokenSource cancellation) : MemoryStream(data)
	{
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			if (this.Position == this.Length)
			{
				cancellation.Cancel();
			}

			return base.ReadAsync(buffer, cancellationToken);
		}
	}

	private sealed class OneByteAtATimeStream(byte[] data) : Stream
	{
		private int position;

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => data.Length;

		public override long Position
		{
			get => this.position;
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (this.position >= data.Length || count == 0)
			{
				return 0;
			}

			buffer[offset] = data[this.position++];
			return 1;
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
