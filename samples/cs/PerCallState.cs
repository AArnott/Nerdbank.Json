using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Json;
using PolyType;
using PolyType.Abstractions;

partial class PerCallStateSamples
{
	// <PerCallState>
	internal static readonly object TenantKey = new();

	async Task SerializeForTenantAsync(JsonSerializer serializer, PipeWriter writer, Invoice invoice, string tenant, CancellationToken cancellationToken)
	{
		// Start from the serializer's own context so its settings and state carry over.
		SerializationContext context = serializer.StartingContext;
		context[TenantKey] = tenant;
		context = context with { CancellationToken = cancellationToken };

		await serializer.SerializeAsync(writer, invoice, TypeShapeResolver.ResolveDynamicOrThrow<Invoice>(), context);
	}
	// </PerCallState>
}

[GenerateShape]
public partial class Invoice
{
	public TenantId? Tenant { get; set; }

	public decimal Total { get; set; }
}

[GenerateShape]
[JsonConverter(typeof(TenantIdConverter))]
public partial class TenantId
{
	public string? Value { get; set; }
}

// <PerCallStateConverter>
// Fills in the tenant from per-call state when the value doesn't specify one.
public sealed class TenantIdConverter : JsonConverter<TenantId>
{
	public override void Write(ref JsonWriter writer, TenantId? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStringValue(value.Value ?? context[PerCallStateSamples.TenantKey] as string);
	}

	public override TenantId? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return null;
		}

		return new TenantId { Value = reader.ReadString() ?? context[PerCallStateSamples.TenantKey] as string };
	}
}
// </PerCallStateConverter>
