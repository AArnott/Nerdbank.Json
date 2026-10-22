// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using PolyType.Abstractions;

public partial class UnusedDataPacketTests : TestBase
{
	private const string Document = """{"name":"Ada","number":1.2300e+04,"future":{"items":[null,true,"\u0041"]}}""";

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task PublicMember_RetainsRawValuesAndRunsCallbacks(bool async)
	{
		PublicModel value = (await this.Read<PublicModel>(Document, async))!;
		Assert.NotNull(value.Packet);
		Assert.True(value.AfterDeserializeSawPacket);
		Assert.Equal(Document, await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task PrivateProperty_RoundTrips(bool async)
	{
		PrivatePropertyModel value = (await this.Read<PrivatePropertyModel>(Document, async))!;
		Assert.Equal(Document, await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task PrivateField_RoundTrips(bool async)
	{
		PrivateFieldModel value = (await this.Read<PrivateFieldModel>(Document, async))!;
		Assert.Equal(Document, await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ConstructorBoundStruct_RoundTrips(bool async)
	{
		ConstructorModel value = await this.Read<ConstructorModel>(Document, async);
		Assert.Equal(Document, await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task MutableStruct_RoundTrips(bool async)
	{
		MutableStruct value = await this.Read<MutableStruct>(Document, async);
		Assert.Equal(Document, await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task PacketOnlyObject_RoundTrips(bool async)
	{
		const string json = """{"number":-0.00,"array":[1,2],"packet":null}""";
		PacketOnlyModel value = (await this.Read<PacketOnlyModel>(json, async))!;
		Assert.Equal(json, await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task GetterOnly_SkipsUnknownDataButCanWriteTransferredPacket(bool async)
	{
		GetterOnlyModel value = (await this.Read<GetterOnlyModel>(Document, async))!;
		Assert.Null(value.Packet);
		Assert.Equal("""{"name":"Ada"}""", await this.Write(value, async));

		PublicModel source = (await this.Read<PublicModel>(Document, async))!;
		GetterOnlyModel target = new() { Name = "Ada" };
		target.SetPacket(source.Packet);
		Assert.Equal(Document, await this.Write(target, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task SetterOnly_CapturesButDoesNotWritePacket(bool async)
	{
		SetterOnlyModel value = (await this.Read<SetterOnlyModel>(Document, async))!;
		Assert.NotNull(value.Captured);
		Assert.Equal("""{"name":"Ada"}""", await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task KnownMembers_DoNotAllocateOrAssignPacket(bool async)
	{
		PublicModel value = (await this.Read<PublicModel>("""{"name":"Ada"}""", async))!;
		Assert.Null(value.Packet);
		Assert.Equal(0, value.PacketAssignments);
		Assert.Equal("""{"name":"Ada"}""", await this.Write(value, async));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Packet_IsAssignedOnceAfterAllProperties(bool async)
	{
		PublicModel value = (await this.Read<PublicModel>(Document, async))!;
		Assert.Equal(1, value.PacketAssignments);
		Assert.Equal("Ada", value.NameWhenPacketAssigned);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	[Arguments(false, true)]
	[Arguments(true, true)]
	public async Task DuplicateUnknownNames_UseConfiguredCollisionRules(bool async, bool ignoreCase = false)
	{
		this.Serializer = this.Serializer with { PropertyNameCaseInsensitive = ignoreCase };
		string json = ignoreCase ? """{"future":1,"FUTURE":2}""" : """{"future":1,"future":2}""";
		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await this.Read<PublicModel>(json, async));
		Assert.Equal(JsonSerializationException.ErrorCode.DoublePropertyAssignment, exception.Code);
	}

	[Test]
	public void MultiplePacketMembers_AreRejected()
	{
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new MultiplePacketsModel()));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ReferenceMetadata_RemainsOutsidePacket(bool async)
	{
		this.Serializer = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		PublicModel value = (await this.Read<PublicModel>("""{"$id":1,"$value":{"name":"Ada","future":{"$ref":"opaque"}}}""", async))!;
		Assert.Equal("""{"$id":1,"$value":{"name":"Ada","future":{"$ref":"opaque"}}}""", await this.Write(value, async));
	}

	private static ITypeShape<T> Shape<T>()
#if NET
		where T : IShapeable<T> => T.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T>();
#endif

	private async Task<T?> Read<T>(string json, bool async)
#if NET
		where T : IShapeable<T>
#endif
	{
		if (!async)
		{
			return this.Serializer.Deserialize<T>(json);
		}

		Pipe pipe = new();
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(json));
		await pipe.Writer.CompleteAsync();
		try
		{
			return await this.Serializer.DeserializeAsync(pipe.Reader, Shape<T>());
		}
		finally
		{
			await pipe.Reader.CompleteAsync();
		}
	}

	private async Task<string> Write<T>(T value, bool async)
#if NET
		where T : IShapeable<T>
#endif
	{
		if (!async)
		{
			return this.Serializer.Serialize(value);
		}

		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, value, Shape<T>());
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	[GenerateShape]
	internal partial struct ConstructorModel(string name)
	{
		public string Name { get; } = name;

		public UnusedDataPacket? Packet { get; set; }
	}

	[GenerateShape]
	internal partial struct MutableStruct
	{
		public string? Name { get; set; }

		public UnusedDataPacket? Packet { get; set; }
	}

	[GenerateShape]
	internal partial class PublicModel : IJsonSerializationCallbacks
	{
		private UnusedDataPacket? packet;

		public string? Name { get; set; }

		public UnusedDataPacket? Packet
		{
			get => this.packet;
			set
			{
				this.PacketAssignments++;
				this.NameWhenPacketAssigned = this.Name;
				this.packet = value;
			}
		}

		internal int PacketAssignments { get; private set; }

		internal string? NameWhenPacketAssigned { get; private set; }

		internal bool AfterDeserializeSawPacket { get; private set; }

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize() => this.AfterDeserializeSawPacket = this.Packet is not null;
	}

	[GenerateShape]
	internal partial class PrivatePropertyModel
	{
		public string? Name { get; set; }

		[PropertyShape]
		private UnusedDataPacket? Packet { get; set; }
	}

	[GenerateShape]
	internal partial class PrivateFieldModel
	{
		[PropertyShape]
#pragma warning disable CS0169 // Accessed through the generated shape.
		private UnusedDataPacket? packet;
#pragma warning restore CS0169

		public string? Name { get; set; }
	}

	[GenerateShape]
	internal partial class PacketOnlyModel
	{
		public UnusedDataPacket? Packet { get; set; }
	}

	[GenerateShape]
	internal partial class GetterOnlyModel
	{
		private UnusedDataPacket? packet;

		public string? Name { get; set; }

		public UnusedDataPacket? Packet => this.packet;

		internal void SetPacket(UnusedDataPacket? value) => this.packet = value;
	}

	[GenerateShape]
	internal partial class SetterOnlyModel
	{
		public string? Name { get; set; }

		public UnusedDataPacket? Packet
		{
			set => this.Captured = value;
		}

		internal UnusedDataPacket? Captured { get; private set; }
	}

	[GenerateShape]
	internal partial class MultiplePacketsModel
	{
		public UnusedDataPacket? First { get; set; }

		public UnusedDataPacket? Second { get; set; }
	}
}
