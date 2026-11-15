// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable DuckTyping // The runtime union test exercises DerivedTypeDuckTyping.
#pragma warning disable SA1201, SA1204 // Nested unions, records, and helpers stay with the tests that use them.

using System.Text;
using PolyType.Abstractions;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

public partial class UnionDiscriminatorTests : TestBase
{
	[Test]
	public void Member_IsWrittenFirst_OptionalOnRead_AndCheckedWhenPresent()
	{
		KindFirst value = this.Roundtrip(new KindFirst { Uri = "file:///a.cs" })!;
		Assert.Equal("file:///a.cs", value.Uri);
		Assert.Equal("create", value.Kind);
		Assert.Equal("""{"kind":"create","uri":"file:///a.cs"}""", this.LastSerializedJson);

		KindFirst missing = this.Read<KindFirst>("""{"uri":"file:///a.cs"}""")!;
		Assert.Equal("file:///a.cs", missing.Uri);
		Assert.Equal("create", missing.Kind);

		KindFirst reordered = this.Read<KindFirst>("""{"uri":"file:///a.cs","kind":"create"}""")!;
		Assert.Equal("file:///a.cs", reordered.Uri);

		JsonSerializationException mismatch = Assert.Throws<JsonSerializationException>(() => this.Read<KindFirst>("""{"kind":"delete","uri":"file:///a.cs"}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.DiscriminatorMismatch, mismatch.Code);

		JsonSerializationException duplicate = Assert.Throws<JsonSerializationException>(() => this.Read<KindFirst>("""{"kind":"create","kind":"create"}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.DoublePropertyAssignment, duplicate.Code);
	}

	[Test]
	public void TypeLevelName_IsNotRenamed()
	{
		TypeLevel value = this.Roundtrip(new TypeLevel { Uri = "file:///a.cs" })!;
		Assert.Equal("file:///a.cs", value.Uri);
		Assert.Equal("""{"Kind":"create","uri":"file:///a.cs"}""", this.LastSerializedJson);
	}

	[Test]
	public void BoolIntAndEnum_RoundTripByNumberAndName()
	{
		Assert.Equal("""{"flag":false,"name":"a"}""", this.Roundtrip(new BoolCase { Name = "a" })!.Name is "a" ? this.LastSerializedJson : null);
		Assert.Equal("""{"code":0}""", this.Roundtrip(new IntCase())!.Code == 0 ? this.LastSerializedJson : null);
		Assert.Equal("""{"color":1}""", this.Roundtrip(new EnumNumber())!.Color == Color.Red ? this.LastSerializedJson : null);

		JsonSerializer named = new() { SerializeEnumValuesByName = true, PropertyNamingPolicy = null };
		Assert.Equal("""{"Color":"Red"}""", named.Serialize(new EnumNumber(), Shape<EnumNumber>()));
		Assert.Equal(Color.Red, named.Deserialize("""{"Color":"Red"}""", Shape<EnumNumber>())!.Color);
		JsonSerializationException mismatch = Assert.Throws<JsonSerializationException>(() => named.Deserialize("""{"Color":"Blue"}""", Shape<EnumNumber>()));
		Assert.Equal(JsonSerializationException.ErrorCode.DiscriminatorMismatch, mismatch.Code);
	}

	[Test]
	public void DefaultOmission_StillWritesTheDiscriminator()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		Assert.Equal("""{"code":0}""", serializer.Serialize(new IntCase(), Shape<IntCase>()));
		Assert.Equal("""{"flag":false}""", serializer.Serialize(new BoolCase(), Shape<BoolCase>()));
	}

	[Test]
	public void ConstField_IsOmittedBecauseTheShapeExcludesIt()
	{
		string json = this.Serializer.Serialize(new ConstFieldCase { Uri = "file:///a.cs" }, Shape<ConstFieldCase>());
		Assert.Equal("""{"uri":"file:///a.cs"}""", json);
	}

	[Test]
	public void PropertyShapeName_IsTheSerializedName()
	{
		Assert.Equal("""{"op":"create","uri":"file:///a.cs"}""", this.Roundtrip(new Renamed { Uri = "file:///a.cs" }) is not null ? this.LastSerializedJson : null);
	}

	[Test]
	public async Task AsyncRoundTrip_PreservesTheDiscriminator()
	{
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, new KindFirst { Uri = "file:///a.cs" }, Shape<KindFirst>());
		Assert.Equal("""{"kind":"create","uri":"file:///a.cs"}""", Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		KindFirst value = (await this.Serializer.DeserializeAsync(stream, Shape<KindFirst>()))!;
		Assert.Equal("file:///a.cs", value.Uri);
	}

	[Test]
	public void Union_WritesTypeThenDiscriminator()
	{
		Assert.Equal("""{"$type":"CreateCase","kind":"create","uri":"file:///a.cs"}""", this.Roundtrip<FileChange>(new CreateCase("file:///a.cs")) is not null ? this.LastSerializedJson : null);
		Assert.Equal("""{"$type":"EditCase","uri":"file:///a.cs"}""", this.Roundtrip<FileChange>(new EditCase("file:///a.cs")) is not null ? this.LastSerializedJson : null);
	}

	[Test]
	public void Schema_ListsDiscriminatorAsOptionalConst()
	{
		JsonSchemaObject schema = this.Schema<KindFirst>();
		JsonSchemaObject properties = (JsonSchemaObject)schema["properties"]!;
		Assert.Equal("kind", properties.First().Key);
		Assert.Equal("create", properties["kind"]!["const"]!.GetValue<string>());
		Assert.Null(schema["required"]);

		JsonSchemaObject draft4 = this.Serializer.GetJsonSchema(Shape<KindFirst>(), new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft4 });
		Assert.Equal("create", draft4["properties"]!["kind"]!["enum"]![0]!.GetValue<string>());
		Assert.Null(draft4["properties"]!["kind"]!["const"]);
	}

	[Test]
	public void InvalidDeclarations_FailWhenTheConverterIsBuilt()
	{
		AssertNotSupported<GetterMismatch>("instead of");
		AssertNotSupported<SetterCase>("setter");
		AssertNotSupported<DoubleCase>("unsupported");
		AssertNotSupported<TwoDiscriminators>("more than one");
		AssertNotSupported<FileChangeMixed>("discriminator names");
		AssertNotSupported<FileChangeDuplicate>("more than one case");
	}

	[Test]
	public void RuntimeUnion_RejectsMixedDiscriminatorNamesWhenBuilt()
	{
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(Shape<IFileNode>(), Shape<CreateCase>(), Shape<DeleteCase>())],
		};
		IFileNode value = new CreateCase("file:///a.cs");
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Serialize(value, Shape<IFileNode>()));
		Assert.IsType<NotSupportedException>(error.InnerException);
		Assert.Contains("discriminator names", error.Message, StringComparison.OrdinalIgnoreCase);
	}

	private static void AssertNotSupported<T>(string message)
#if NET
		where T : IShapeable<T>
#endif
	{
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => new JsonSerializer().Serialize(default(T), Shape<T>()));
		Assert.IsType<NotSupportedException>(error.InnerException);
		Assert.Contains(message, error.InnerException.Message, StringComparison.OrdinalIgnoreCase);
	}

	private T? Read<T>(string json)
#if NET
		where T : IShapeable<T> => this.Serializer.Deserialize<T>(json);
#else
		=> this.Serializer.Deserialize(json, Shape<T>());
#endif

	private JsonSchemaObject Schema<T>()
#if NET
		where T : IShapeable<T> => this.Serializer.GetJsonSchema(Shape<T>());
#else
		=> this.Serializer.GetJsonSchema(Shape<T>());
#endif

	private static ITypeShape<T> Shape<T>()
#if NET
		where T : IShapeable<T> => T.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T>();
#endif

	[GenerateShape]
	public partial class KindFirst
	{
		public string? Uri { get; set; }

		[UnionDiscriminator("create")]
		public string Kind => "create";
	}

	[GenerateShape]
	[UnionDiscriminator("Kind", "create")]
	public partial class TypeLevel
	{
		public string? Uri { get; set; }
	}

	[GenerateShape]
	public partial class BoolCase
	{
		[UnionDiscriminator(false)]
		public bool Flag => false;

		public string? Name { get; set; }
	}

	[GenerateShape]
	public partial class IntCase
	{
		[UnionDiscriminator(0)]
		public int Code => 0;
	}

	[GenerateShape]
	public partial class EnumNumber
	{
		[UnionDiscriminator(Color.Red)]
		public Color Color => Color.Red;
	}

	[GenerateShape]
	public partial class ConstFieldCase
	{
		[UnionDiscriminator("create")]
		public const string Kind = "create";

		public string? Uri { get; set; }
	}

	[GenerateShape]
	public partial class Renamed
	{
		public string? Uri { get; set; }

		[PropertyShape(Name = "op")]
		[UnionDiscriminator("create")]
		public string Kind => "create";
	}

	[GenerateShape]
	public partial class GetterMismatch
	{
		[UnionDiscriminator("nope")]
		public string Kind => "create";
	}

	[GenerateShape]
	public partial class SetterCase
	{
		[UnionDiscriminator("create")]
		public string? Kind { get; set; }
	}

	[GenerateShape]
	public partial class DoubleCase
	{
		[UnionDiscriminator(1.5)]
		public double Kind => 1.5;
	}

	[GenerateShape]
	public partial class TwoDiscriminators
	{
		[UnionDiscriminator("create")]
		public string Kind => "create";

		[UnionDiscriminator("delete")]
		public string Other => "delete";
	}

	[GenerateShape]
	public partial union FileChange(CreateCase, EditCase);

	[GenerateShape]
	public partial record CreateCase(string Uri) : IFileNode
	{
		[UnionDiscriminator("create")]
		public string Kind => "create";
	}

	[GenerateShape]
	public partial record EditCase(string Uri);

	[GenerateShape]
	public partial record DeleteCase(string Uri) : IFileNode
	{
		[UnionDiscriminator("delete")]
		public string Op => "delete";
	}

	[GenerateShape]
	public partial interface IFileNode;

	[GenerateShape]
	public partial union FileChangeMixed(CreateCase, DeleteCase);

	[GenerateShape]
	public partial union FileChangeDuplicate(CreateCase, DuplicateCase);

	[GenerateShape]
	public partial record DuplicateCase(string Uri)
	{
		[UnionDiscriminator("create")]
		public string Kind => "create";
	}

	public enum Color
	{
		Red = 1,
		Blue = 2,
	}
}
