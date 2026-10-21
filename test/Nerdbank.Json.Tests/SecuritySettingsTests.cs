// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Dynamic;
using System.Text;
using PolyType.Abstractions;

[GenerateShapeFor<ExpandoObject>]
public partial class SecuritySettingsTests
{
	[Test]
	public void DefaultCtorMatchesUntrustedData()
	{
		Assert.Equal(SecuritySettings.UntrustedData, new SecuritySettings());
	}

	[Test]
	public void TrustedDataExists()
	{
		Assert.Equal(int.MaxValue, SecuritySettings.TrustedData.ExpandoObjectMaxPropertyCount);
		Assert.Equal(int.MaxValue, SecuritySettings.TrustedData.MaxObjectMemberCount);
		Assert.Equal(128, SecuritySettings.UntrustedData.ExpandoObjectMaxPropertyCount);
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	public void LimitsMustBePositive(int value)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new SecuritySettings { ExpandoObjectMaxPropertyCount = value });
		Assert.Throws<ArgumentOutOfRangeException>(() => new SecuritySettings { MaxObjectMemberCount = value });
	}

	[Test]
	[Arguments(128)]
	[Arguments(129)]
	public async Task ExpandoLimit_AppliesToReadAndWrite(int count)
	{
		JsonSerializer trusted = new JsonSerializer
		{
			StartingContext = new() { Security = SecuritySettings.TrustedData },
		}.WithExpandoObjectConverter();
		JsonSerializer untrusted = new JsonSerializer().WithExpandoObjectConverter();
		ExpandoObject value = new();
		IDictionary<string, object?> members = value;
		for (int i = 0; i < count; i++)
		{
			members.Add("p" + i, i);
		}

		string json = trusted.Serialize<ExpandoObject, SecuritySettingsTests>(value);
		using MemoryStream input = new(Encoding.UTF8.GetBytes(json));
		using MemoryStream output = new();
		ITypeShape<ExpandoObject> shape = TypeShapeResolver.ResolveDynamicOrThrow<ExpandoObject, SecuritySettingsTests>();
		if (count == 128)
		{
			Assert.Equal(count, ((IDictionary<string, object?>)untrusted.Deserialize(json, shape)!).Count);
			Assert.Equal(count, ((IDictionary<string, object?>)(await untrusted.DeserializeAsync(input, shape))!).Count);
			await untrusted.SerializeAsync(output, value, shape);
			Assert.Equal(json, Encoding.UTF8.GetString(output.ToArray()));
		}
		else
		{
			Assert.Throws<JsonSerializationException>(() => untrusted.Deserialize(json, shape));
			await Assert.ThrowsAsync<FormatException>(async () => await untrusted.DeserializeAsync(input, shape));
			Assert.Throws<JsonSerializationException>(() => untrusted.Serialize(value, shape));
			await Assert.ThrowsAsync<FormatException>(async () => await untrusted.SerializeAsync(output, value, shape));
		}
	}

	[Test]
	[Arguments(1)]
	[Arguments(2)]
	public async Task ExpandoObjectMemberLimit_AppliesToReadAndWrite(int count)
	{
		JsonSerializer serializer = new JsonSerializer
		{
			StartingContext = new() { Security = new() { MaxObjectMemberCount = 1 } },
		}.WithExpandoObjectConverter();
		ExpandoObject value = new();
		IDictionary<string, object?> members = value;
		members.Add("a", 1);
		if (count == 2)
		{
			members.Add("b", 2);
		}

		string json = count == 1 ? """{"a":1}""" : """{"a":1,"b":2}""";
		ITypeShape<ExpandoObject> shape = TypeShapeResolver.ResolveDynamicOrThrow<ExpandoObject, SecuritySettingsTests>();
		using MemoryStream input = new(Encoding.UTF8.GetBytes(json));
		using MemoryStream output = new();
		if (count == 1)
		{
			Assert.Single((IDictionary<string, object?>)serializer.Deserialize(json, shape)!);
			Assert.Single((IDictionary<string, object?>)(await serializer.DeserializeAsync(input, shape))!);
			Assert.Equal(json, serializer.Serialize(value, shape));
			await serializer.SerializeAsync(output, value, shape);
			Assert.Equal(json, Encoding.UTF8.GetString(output.ToArray()));
		}
		else
		{
			Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(json, shape));
			await Assert.ThrowsAsync<FormatException>(async () => await serializer.DeserializeAsync(input, shape));
			Assert.Throws<JsonSerializationException>(() => serializer.Serialize(value, shape));
			await Assert.ThrowsAsync<FormatException>(async () => await serializer.SerializeAsync(output, value, shape));
		}
	}

	[Test]
	public void Expando_DuplicatesAndNestedDepthAreStillRejected()
	{
		JsonSerializer serializer = new JsonSerializer
		{
			StartingContext = new() { MaxDepth = 2 },
		}.WithExpandoObjectConverter();
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize<ExpandoObject, SecuritySettingsTests>("""{"a":1,"a":2}"""));
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize<ExpandoObject, SecuritySettingsTests>("""{"a":{"b":{"c":1}}}"""));
	}
}
