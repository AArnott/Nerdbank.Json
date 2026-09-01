// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PolyType;

namespace Nerdbank.Json.AspNetCore.Tests;

public class IntegrationTests
{
	private static readonly ITypeShapeProvider Provider =
		PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_AspNetCoreMvcFormatter_Tests.Default;

	[Test]
	public async Task Post_RoundTripsThroughNerdbankFormatter()
	{
		using IHost host = await CreateHostAsync(replaceSystemTextJson: true);
		using HttpClient client = host.GetTestClient();

		using StringContent content = new("""{"name":"Ada","age":36}""", Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await client.PostAsync("/people/echo", content);

		response.EnsureSuccessStatusCode();
		Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
		Assert.Equal("""{"name":"Ada","age":36}""", await response.Content.ReadAsStringAsync());
	}

	[Test]
	public async Task Get_ReturnsNerdbankSerializedJson()
	{
		using IHost host = await CreateHostAsync(replaceSystemTextJson: true);
		using HttpClient client = host.GetTestClient();

		using HttpResponseMessage response = await client.GetAsync("/people/sample");

		response.EnsureSuccessStatusCode();
		Assert.Equal("""{"name":"Grace","age":45}""", await response.Content.ReadAsStringAsync());
	}

	[Test]
	public async Task MalformedJson_ReturnsBadRequest()
	{
		// Keep the built-in formatters so ProblemDetails (which has no generated shape) can still be written.
		using IHost host = await CreateHostAsync(replaceSystemTextJson: false);
		using HttpClient client = host.GetTestClient();

		using StringContent content = new("""{"name":"Ada","age":}""", Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await client.PostAsync("/people/echo", content);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Test]
	public async Task ChunkedRequestBody_Deserializes()
	{
		using IHost host = await CreateHostAsync(replaceSystemTextJson: true);
		using HttpClient client = host.GetTestClient();

		using StreamContent content = new(new SlowStream(Encoding.UTF8.GetBytes("""{"name":"Ada","age":36}""")));
		content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
		using HttpResponseMessage response = await client.PostAsync("/people/echo", content);

		response.EnsureSuccessStatusCode();
		Assert.Equal("""{"name":"Ada","age":36}""", await response.Content.ReadAsStringAsync());
	}

	private static async Task<IHost> CreateHostAsync(bool replaceSystemTextJson)
		=> await new HostBuilder()
			.ConfigureWebHost(web =>
			{
				web.UseTestServer()
					.ConfigureServices(services =>
					{
						services
							.AddControllers()
							.AddNerdbankJsonFormatters(new JsonSerializer(), Provider, o => o.ReplaceSystemTextJsonFormatters = replaceSystemTextJson);
					})
					.Configure(app =>
					{
						app.UseRouting();
						app.UseEndpoints(endpoints => endpoints.MapControllers());
					});
			})
			.StartAsync();

	private sealed class SlowStream(byte[] data) : Stream
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

[ApiController]
[Route("people")]
public class PeopleController : ControllerBase
{
	[HttpPost("echo")]
	public Person Echo([FromBody] Person person) => person;

	[HttpGet("sample")]
	public Person Sample() => new("Grace", 45);
}
