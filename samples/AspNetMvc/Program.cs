// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Nerdbank.Json;
using PolyType;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// <Registration>
// A single, reusable serializer. Configure naming policies, security limits, converters, etc. here.
JsonSerializer serializer = new();

// Register the Nerdbank.Json MVC formatters with the source-generated shape provider for this assembly.
// Because the provider is supplied explicitly, no reflection is used and the app stays trimming/NativeAOT friendly.
// Setting ReplaceSystemTextJsonFormatters makes Nerdbank.Json the only JSON formatter; omit it to run alongside
// the built-in System.Text.Json formatters (Nerdbank.Json still wins content negotiation for the types it knows).
builder.Services
	.AddControllers()
	.AddNerdbankJsonFormatters(
		serializer,
		PolyType.SourceGenerator.TypeShapeProvider_AspNetMvc.Default,
		options => options.ReplaceSystemTextJsonFormatters = true);
// </Registration>

WebApplication app = builder.Build();
app.MapControllers();
app.Run();

/// <summary>A sample model. The <see cref="GenerateShapeAttribute"/> makes it visible to the shape provider.</summary>
/// <param name="TemperatureC">The temperature in Celsius.</param>
/// <param name="Summary">A human-readable summary.</param>
[GenerateShape]
public partial record WeatherForecast(int TemperatureC, string Summary);

/// <summary>A witness that adds the array shape used by the collection-returning action.</summary>
[GenerateShapeFor<WeatherForecast[]>]
public partial class SampleWitness;

/// <summary>A controller whose JSON is read and written by the Nerdbank.Json formatters.</summary>
[ApiController]
[Route("weather")]
public class WeatherController : ControllerBase
{
	/// <summary>Returns a small forecast collection, serialized by Nerdbank.Json.</summary>
	/// <returns>The forecasts.</returns>
	[HttpGet]
	public WeatherForecast[] Get() =>
	[
		new WeatherForecast(22, "Sunny"),
		new WeatherForecast(17, "Cloudy"),
	];

	/// <summary>Echoes a posted forecast, deserialized and reserialized by Nerdbank.Json.</summary>
	/// <param name="forecast">The posted forecast.</param>
	/// <returns>The same forecast.</returns>
	[HttpPost]
	public WeatherForecast Echo([FromBody] WeatherForecast forecast) => forecast;
}
