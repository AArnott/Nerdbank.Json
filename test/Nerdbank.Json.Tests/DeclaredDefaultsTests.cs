// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;
using PolyType.Abstractions;

public partial class DeclaredDefaultsTests : TestBase
{
	[Test]
	public async Task DeclaredDefaults_AreOmittedAndRoundtrip()
	{
		this.Serializer = this.Serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		Defaults value = new();
		Assert.Equal("{}", this.Serializer.Serialize(value));
		Defaults result = this.Serializer.Deserialize<Defaults>("{}")!;
		Assert.Equal(42, result.Number);
		Assert.Equal("hello", result.Text);
		Assert.Equal(7, result.Optional);
		Assert.Null(result.Nullable);

		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, value, TypeShapeResolver.ResolveDynamicOrThrow<Defaults>());
		Assert.Equal("{}", Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		result = (await this.Serializer.DeserializeAsync(stream, TypeShapeResolver.ResolveDynamicOrThrow<Defaults>()))!;
		Assert.Equal(42, result.Number);
		Assert.Equal(7, result.Optional);
	}

	[Test]
	public void ClrDefaults_AreNotOmittedWhenDeclaredDefaultDiffers()
	{
		this.Serializer = this.Serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		Assert.Equal("""{"optional":0,"number":0,"text":null}""", this.Serializer.Serialize(new Defaults(0) { Number = 0, Text = null }));
	}

	[Test]
	public void ConstructorDefault_TakesPrecedenceOverAttribute()
	{
		this.Serializer = this.Serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		Assert.Equal("{}", this.Serializer.Serialize(new Precedence()));
		Assert.Equal("""{"number":9}""", this.Serializer.Serialize(new Precedence(9)));
	}

	[Test]
	public void GetterOnlyScalar_WithoutConstructorBinding_IsIgnored()
	{
		Assert.Equal("{}", this.Serializer.Serialize(new Computed()));
	}

	[Test]
	public void RequiredShapeMetadata_IsHonored()
	{
		this.Serializer = this.Serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Required };
		Assert.Equal("""{"number":0}""", this.Serializer.Serialize(new ShapeRequired { Number = 0 }));
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<ShapeRequired>("{}"));
	}

	[Test]
	public void NullableValueType_UsesValueTypesPolicy()
	{
		this.Serializer = this.Serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.ValueTypes };
		Assert.Contains("\"nullable\":null", this.Serializer.Serialize(new Defaults()), StringComparison.Ordinal);
		this.Serializer = this.Serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.ReferenceTypes };
		Assert.DoesNotContain("\"nullable\"", this.Serializer.Serialize(new Defaults()), StringComparison.Ordinal);
	}

	[Test]
	public void InitializerWithoutAttribute_DoesNotChangeDefault()
	{
		this.Serializer = this.Serializer with { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		Assert.Equal("""{"number":42}""", this.Serializer.Serialize(new Initializer()));
		Assert.Equal("{}", this.Serializer.Serialize(new Initializer { Number = 0 }));
	}

	[Test]
	public async Task NonNullableProperty_RejectsNull_SyncAndAsync()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<NonNullable>("""{"text":null}"""));
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""{"text":null}"""));
		await Assert.ThrowsAsync<FormatException>(async () => await this.Serializer.DeserializeAsync(stream, TypeShapeResolver.ResolveDynamicOrThrow<NonNullable>()));
	}

	[GenerateShape]
	internal partial class Defaults(int optional = 7)
	{
		public int Optional { get; } = optional;

		[DefaultValue(42)]
		public int Number { get; set; } = 42;

		[DefaultValue("hello")]
		public string? Text { get; set; } = "hello";

		public int? Nullable { get; set; }
	}

	[GenerateShape]
	internal partial class Precedence(int number = 5)
	{
		[DefaultValue(9)]
		public int Number { get; } = number;
	}

	[GenerateShape]
	internal partial class Computed
	{
		public int Number => 42;
	}

	[GenerateShape]
	internal partial class ShapeRequired
	{
		[PropertyShape(IsRequired = true)]
		public int Number { get; set; }
	}

	[GenerateShape]
	internal partial class Initializer
	{
		public int Number { get; set; } = 42;
	}

	[GenerateShape]
	internal partial class NonNullable
	{
		public string Text { get; set; } = string.Empty;
	}
}
