using System.Collections.Generic;
using Nerdbank.Json;
using Nerdbank.MessagePack;
using PolyType;

partial class Security
{
	void SetMaxDepth()
	{
		#region SetMaxDepth
		var serializer = new JsonSerializer
		{
			StartingContext = new SerializationContext
			{
				MaxDepth = 100,
			},
		};
		#endregion
	}

	void SetSecuritySettings_TrustedData()
	{
		#region SetSecuritySettings_TrustedData
		var serializer = new JsonSerializer
		{
			ComparerProvider = null,
			StartingContext = new SerializationContext
			{
				Security = SecuritySettings.TrustedData,
			},
		};
		#endregion
	}

	void SetSecuritySettings_Custom()
	{
		#region SetSecuritySettings_Custom
		var serializer = new JsonSerializer
		{
			ComparerProvider = SecureComparerProvider.Default,
			StartingContext = new SerializationContext
			{
				MaxDepth = 100,
				Security = new SecuritySettings(),
			},
		};
		#endregion
	}

	#region SecureEqualityComparers
	[GenerateShape]
	public partial class HashCollisionResistance
	{
		public Dictionary<CustomType, string> Dictionary { get; } = new(StructuralEqualityComparer.GetHashCollisionResistant<CustomType>());

		public HashSet<CustomType> HashSet { get; } = new(StructuralEqualityComparer.GetHashCollisionResistant<CustomType>());
	}

	[GenerateShape]
	public partial class CustomType
	{
		public string? Value { get; set; }
	}
	#endregion
}
