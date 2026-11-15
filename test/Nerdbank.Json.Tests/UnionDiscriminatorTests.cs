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
		JsonSerializationException unknown = Assert.Throws<JsonSerializationException>(() => named.Deserialize("""{"Color":"Missing"}""", Shape<EnumNumber>()));
		Assert.Equal(JsonSerializationException.ErrorCode.DiscriminatorMismatch, unknown.Code);
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
	public void Union_WritesDiscriminatedCaseBareAndWrapsTheAmbiguousCase()
	{
		Assert.Equal("""{"kind":"create","uri":"file:///a.cs"}""", this.Roundtrip<FileChange>(new CreateCase("file:///a.cs")) is not null ? this.LastSerializedJson : null);
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
	public void MemberNameCollision_FailsWhenTheConverterIsBuilt()
		=> AssertNotSupported<MemberNameCollision>("already has a property");

	[Test]
	public void NullConstant_FailsWhenTheConverterIsBuilt()
		=> AssertNotSupported<NullKind>("cannot be null");

	[Test]
	public void RecursiveUnion_RejectsDuplicateDiscriminatorValues()
		=> AssertNotSupported<RecursiveHost>("more than one case");

	[Test]
	public void ReferencePreservation_RejectsDuplicateDiscriminatorValues()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.AllowCycles };
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Serialize(new CreateCase("file:///a.cs"), Shape<FileChangeDuplicate>()));
		Assert.IsType<NotSupportedException>(error.InnerException);
		Assert.Contains("more than one case", error.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void PrivateField_DoesNotCollideWithTheTypeLevelName()
	{
		PrivateKindField value = this.Roundtrip(new PrivateKindField { Uri = "file:///a.cs" })!;
		Assert.Equal("file:///a.cs", value.Uri);
		Assert.Equal("""{"kind":"create","uri":"file:///a.cs"}""", this.LastSerializedJson);
	}

	[Test]
	public void UnsignedAndUnnamedEnums_KeepTheirJsonKind()
	{
		Assert.Equal("""{"code":18446744073709551615,"value":0}""", this.Roundtrip(new BigEnumCase()) is not null ? this.LastSerializedJson : null);
		JsonSerializer named = new() { SerializeEnumValuesByName = true, PropertyNamingPolicy = null };
		Assert.Equal("""{"Code":"High"}""", named.Serialize(new BigEnumMember(), Shape<BigEnumMember>()));
		Assert.Equal("""{"perm":3,"X":1}""", named.Serialize(new FlagType { X = 1 }, Shape<FlagType>()));
		Assert.Equal(18446744073709551615UL, this.Schema<BigEnumCase>()["properties"]!["code"]!["const"]!.GetValue<ulong>());
	}

	[Test]
	public void OutOfRangeEnumConstant_IsRejected()
		=> AssertNotSupported<WideEnum>("does not fit");

	[Test]
	public void OutOfRangeNumber_IsADiscriminatorMismatch()
	{
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => this.Read<IntCase>("""{"code":2147483648}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.DiscriminatorMismatch, error.Code);
	}

	[Test]
	public void ConverterCapture_UsesAnOperationContextAndJsonKind()
	{
		FractionalCode value = this.Roundtrip(new FractionalCode())!;
		Assert.Equal(7, value.Code);
		Assert.Equal("""{"code":7.5}""", this.LastSerializedJson);
		Assert.Equal("number", this.Schema<FractionalCode>()["properties"]!["code"]!["type"]!.GetValue<string>());
	}

	[Test]
	public void PropertyConverter_DefinesTheWireValueAndSchema()
	{
		TextCode value = this.Roundtrip(new TextCode())!;
		Assert.Equal(7, value.Code);
		Assert.Equal("""{"code":"7"}""", this.LastSerializedJson);
		Assert.Equal("string", this.Schema<TextCode>()["properties"]!["code"]!["type"]!.GetValue<string>());
		Assert.Equal("7", this.Schema<TextCode>()["properties"]!["code"]!["const"]!.GetValue<string>());
	}

	[Test]
	public void ConverterOutput_CollidesWithAStringDiscriminator()
		=> AssertNotSupported<TextCollision>("more than one case");

	[Test]
	public void TypeLevelEnum_FollowsTheEnumConverterRules()
	{
		JsonSerializer named = new() { SerializeEnumValuesByName = true, PropertyNamingPolicy = null };
		Assert.NotNull(named.Deserialize("""{"color":"RED"}""", Shape<EnumLevel>()));
		Assert.NotNull(named.Deserialize("""{"color":1}""", Shape<EnumLevel>()));
		JsonSerializationException collision = Assert.Throws<JsonSerializationException>(() => named.Deserialize("""{"color":"RED"}""", Shape<EnumCollision>()));
		Assert.Equal(JsonSerializationException.ErrorCode.DiscriminatorMismatch, collision.Code);
		Assert.NotNull(named.Deserialize("""{"color":"Red"}""", Shape<EnumCollision>()));
	}

	[Test]
	public async Task AsyncConverter_IsUsedWhenSerializingAsynchronously()
	{
		JsonSerializer serializer = new();
		Assert.Equal("""{"code":1}""", serializer.Serialize(new AsyncCode(), Shape<AsyncCode>()));
		AsyncIntConverter.SyncWrites = 0;
		AsyncIntConverter.AsyncWrites = 0;
		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, new AsyncCode(), Shape<AsyncCode>());
		Assert.Equal(0, AsyncIntConverter.SyncWrites);
		Assert.True(AsyncIntConverter.AsyncWrites > 0);
		Assert.Equal("""{"code":1}""", Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Test]
	public void ProbeInstance_DoesNotFinalizeUninitializedState()
	{
		this.Roundtrip(new FinalizerProbe());
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		GC.WaitForPendingFinalizers();
		Assert.Equal(0, FinalizerProbe.UninitializedFinalizations);
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

	[GenerateShape]
	public partial class MemberNameCollision
	{
		[PropertyShape(Name = "kind")]
		public string? Label { get; set; }

		[UnionDiscriminator("create")]
		public string Kind => "create";
	}

	[GenerateShape]
	public partial class NullKind
	{
		[UnionDiscriminator]
		public string? Kind => null;
	}

	[GenerateShape]
	public partial class RecursiveHost
	{
		[UnionDiscriminator("same")]
		public string Kind => "same";

		public RecursiveChoice? Choice { get; set; }
	}

	[GenerateShape]
	public partial class RecursivePeer
	{
		[UnionDiscriminator("same")]
		public string Kind => "same";
	}

	[GenerateShape]
	public partial union RecursiveChoice(RecursiveHost, RecursivePeer);

	[GenerateShape]
	[UnionDiscriminator("kind", "create")]
	public partial class PrivateKindField
	{
		private string kind = "hidden";

		public string? Uri { get; set; }

		public string Hidden => this.kind;
	}

	[GenerateShape]
	[UnionDiscriminator("code", BigColor.High)]
	public partial class BigEnumCase
	{
		public int Value { get; set; }
	}

	[GenerateShape]
	public partial class BigEnumMember
	{
		[UnionDiscriminator(BigColor.High)]
		public BigColor Code => BigColor.High;
	}

	[Flags]
	public enum Perm
	{
		Read = 1,
		Write = 2,
	}

	[GenerateShape]
	[UnionDiscriminator("perm", (Perm)3)]
	public partial class FlagType
	{
		public int X { get; set; }
	}

	public enum BigColor : ulong
	{
		High = ulong.MaxValue,
	}

	[GenerateShape]
	public partial class WideEnum
	{
		[UnionDiscriminator(256)]
		public ByteColor Code => 0;
	}

	public enum ByteColor : byte
	{
		None = 0,
	}

	[GenerateShape]
	public partial class TextCode
	{
		[JsonConverter(typeof(TextIntConverter))]
		[UnionDiscriminator(7)]
		public int Code => 7;
	}

	[GenerateShape]
	[UnionDiscriminator("code", "7")]
	public partial class TextStringCase
	{
		public int X { get; set; }
	}

	[GenerateShape]
	public partial class TextIntCase
	{
		[JsonConverter(typeof(TextIntConverter))]
		[UnionDiscriminator(7)]
		public int Code => 7;
	}

	[GenerateShape]
	public partial union TextCollision(TextStringCase, TextIntCase);

	[GenerateShape]
	[UnionDiscriminator("color", Color.Red)]
	public partial class EnumLevel
	{
		public int X { get; set; }
	}

	public enum CollidingColor
	{
		Red = 1,
		RED = 2,
	}

	[GenerateShape]
	[UnionDiscriminator("color", CollidingColor.Red)]
	public partial class EnumCollision
	{
		public int X { get; set; }
	}

	[GenerateShape]
	public partial class AsyncCode
	{
		[JsonConverter(typeof(AsyncIntConverter))]
		[UnionDiscriminator(1)]
		public int Code => 1;
	}

	[GenerateShape]
	public partial class FractionalCode
	{
		[JsonConverter(typeof(ContextIntConverter))]
		[UnionDiscriminator(7)]
		public int Code => 7;
	}

	public sealed class ContextIntConverter : JsonConverter<int>
	{
		public override void Write(ref JsonWriter writer, in int value, SerializationContext context)
		{
			context.DepthStep();
			_ = context.GetConverter(typeof(int));
			writer.Write(value + 0.5);
		}

		public override int Read(ref JsonReader reader, SerializationContext context)
		{
			context.DepthStep();
			return (int)reader.ReadDouble();
		}
	}

	public sealed class TextIntConverter : JsonConverter<int>
	{
		public override void Write(ref JsonWriter writer, in int value, SerializationContext context)
			=> writer.Write(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

		public override int Read(ref JsonReader reader, SerializationContext context)
			=> int.Parse(reader.ReadRequiredString(), System.Globalization.CultureInfo.InvariantCulture);
	}

	public sealed class AsyncIntConverter : JsonConverter<int>
	{
		public static int SyncWrites;

		public static int AsyncWrites;

		public override bool PreferAsyncSerialization => true;

		public override void Write(ref JsonWriter writer, in int value, SerializationContext context)
		{
			SyncWrites++;
			writer.Write(value);
		}

		public override int Read(ref JsonReader reader, SerializationContext context)
			=> reader.ReadInt32();

		public override ValueTask WriteAsync(JsonAsyncWriter writer, int value, SerializationContext context)
		{
			AsyncWrites++;
			JsonWriter sync = writer.CreateWriter();
			sync.Write(value);
			writer.ReturnWriter(ref sync);
			return default;
		}
	}

	[GenerateShape]
	public partial class FinalizerProbe
	{
		internal static int UninitializedFinalizations;

		private readonly object? resource = new();

		[UnionDiscriminator("create")]
		public string Kind => "create";

		~FinalizerProbe()
		{
			if (this.resource is null)
			{
				Interlocked.Increment(ref UninitializedFinalizations);
			}
		}
	}
}
