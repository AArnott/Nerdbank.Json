using Nerdbank.Json;
using PolyType;

// <SerializationCallbacks>
[GenerateShape]
public partial class Order : IJsonSerializationCallbacks
{
	public decimal Subtotal { get; set; }

	public decimal Tax { get; set; }

	public decimal Total { get; private set; }

	public void OnBeforeSerialize() => this.Validate();

	public void OnAfterDeserialize()
	{
		this.Total = this.Subtotal + this.Tax;
		this.Validate();
	}

	private void Validate()
	{
		if (this.Subtotal < 0 || this.Tax < 0)
		{
			throw new InvalidOperationException("Order amounts cannot be negative.");
		}
	}
}

JsonSerializer serializer = new();
Order copy = serializer.Deserialize<Order>(serializer.Serialize(new Order { Subtotal = 10, Tax = 0.80m }))!;
// </SerializationCallbacks>
