// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Nerdbank.Json.AspNetCoreMvcFormatter.Tests;

public class OutputFormatterTests : FormatterTestBase
{
	private readonly JsonOutputFormatter formatter = new(Provider);

	[Test]
	public void Constructors_ValidateArguments()
	{
		Assert.Throws<ArgumentNullException>(() => new JsonOutputFormatter(null!));
		Assert.Throws<ArgumentNullException>(() => new JsonOutputFormatter(null!, new JsonSerializer()));
		Assert.Throws<ArgumentNullException>(() => new JsonOutputFormatter(Provider, null!));
	}

	[Test]
	public async Task WriteAsync_SerializesModel()
	{
		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(Person), new Person("Ada", 36));

		await this.formatter.WriteAsync(context);

		Assert.Equal("""{"name":"Ada","age":36}""", Encoding.UTF8.GetString(body.ToArray()));
	}

	[Test]
	public async Task WriteAsync_Null_WritesJsonNull()
	{
		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(Person), value: null);

		await this.formatter.WriteAsync(context);

		Assert.Equal("null", Encoding.UTF8.GetString(body.ToArray()));
	}

	[Test]
	public async Task WriteAsync_Utf16Charset()
	{
		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(Person), new Person("Zoë", 7), "application/json; charset=utf-16");

		await this.formatter.WriteAsync(context);

		Assert.Equal("""{"name":"Zoë","age":7}""", Encoding.Unicode.GetString(body.ToArray()));
	}

	[Test]
	public async Task WriteAsync_HonorsSerializerOptions()
	{
		JsonOutputFormatter indenting = new(Provider, new JsonSerializer { WriteIndented = true });
		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(Person), new Person("Ada", 36));

		await indenting.WriteAsync(context);

		string json = Encoding.UTF8.GetString(body.ToArray());
		Assert.Contains("\n", json);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task WriteAsync_RuntimeType_LeavesBodyOpen(bool utf16)
	{
		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(object), new Person("Ada", 36), utf16 ? "application/json; charset=utf-16" : "application/json");

		await this.formatter.WriteAsync(context);

		Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
		Assert.Equal("""{"name":"Ada","age":36}""", encoding.GetString(body.ToArray()));
		Assert.True(body.CanWrite);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task WriteAsync_Cancellation_Throws(bool utf16)
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();
		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(Person), new Person("Ada", 36), utf16 ? "application/json; charset=utf-16" : "application/json", cts.Token);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await this.formatter.WriteAsync(context));

		Assert.True(body.CanWrite);
	}

	[Test]
	public async Task WriteAsync_ProviderMiss_Throws()
	{
		(OutputFormatterWriteContext context, _) = CreateOutputContext(typeof(Unregistered), new Unregistered { X = 1 });

		await Assert.ThrowsAsync<MissingTypeShapeException>(async () => await this.formatter.WriteAsync(context));
	}

	[Test]
	public async Task WriteAsync_LargeGraph()
	{
		int[] numbers = new int[5000];
		for (int i = 0; i < numbers.Length; i++)
		{
			numbers[i] = i;
		}

		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(Bag), new Bag(numbers));

		await this.formatter.WriteAsync(context);

		string json = Encoding.UTF8.GetString(body.ToArray());
		Assert.StartsWith("""{"numbers":[0,1,2,""", json);
		Assert.EndsWith("4999]}", json);
	}

	[Test]
	[Arguments("application/json")]
	[Arguments("text/json")]
	public void CanWriteResult_MediaType(string contentType)
	{
		(OutputFormatterWriteContext context, _) = CreateOutputContext(typeof(Person), new Person("Ada", 1), contentType);
		Assert.True(this.formatter.CanWriteResult(context));
	}
}
