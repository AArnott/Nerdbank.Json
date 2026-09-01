// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Nerdbank.Json.AspNetCore.Tests;

public class OutputFormatterTests : FormatterTestBase
{
	private readonly NerdbankJsonOutputFormatter formatter = new(new JsonSerializer(), Provider);

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
		NerdbankJsonOutputFormatter indenting = new(new JsonSerializer { WriteIndented = true }, Provider);
		(OutputFormatterWriteContext context, MemoryStream body) = CreateOutputContext(typeof(Person), new Person("Ada", 36));

		await indenting.WriteAsync(context);

		string json = Encoding.UTF8.GetString(body.ToArray());
		Assert.Contains("\n", json);
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
