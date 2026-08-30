// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Nerdbank.Json.AspNetCore.Tests;

public class InputFormatterTests : FormatterTestBase
{
	private readonly NerdbankJsonInputFormatter formatter = new(new JsonSerializer(), Provider);

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
	public async Task ReadAsync_ProviderMiss_Throws()
	{
		byte[] body = Encoding.UTF8.GetBytes("""{"x":1}""");
		InputFormatterContext context = CreateInputContext(new MemoryStream(body), "application/json", typeof(Unregistered));

		await Assert.ThrowsAsync<MissingTypeShapeException>(async () => await this.formatter.ReadAsync(context));
	}

	[Test]
	public async Task ReadAsync_Cancellation_Throws()
	{
		byte[] body = Encoding.UTF8.GetBytes("""{"name":"Ada","age":36}""");
		using CancellationTokenSource cts = new();
		cts.Cancel();
		InputFormatterContext context = CreateInputContext(new MemoryStream(body), "application/json", typeof(Person), requestAborted: cts.Token);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await this.formatter.ReadAsync(context));
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
