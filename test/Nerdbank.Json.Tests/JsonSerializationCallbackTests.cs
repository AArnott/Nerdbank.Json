// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;

public partial class JsonSerializationCallbackTests : TestBase
{
	[Test]
	public void MutableObject_CallbacksRunAtDocumentBoundaries()
	{
		CallbackObject value = new() { Value = " initial " };

		string json = this.Serializer.Serialize(value);
		CallbackObject deserialized = this.Serializer.Deserialize<CallbackObject>("""{"value":"wire"}""")!;

		Assert.Equal("""{"value":"prepared"}""", json);
		Assert.Equal(1, value.BeforeSerializeCount);
		Assert.Equal(1, value.AfterSerializeCount);
		Assert.Equal(0, value.BeforeDeserializeCount);
		Assert.Equal(0, value.AfterDeserializeCount);
		Assert.Equal("restored", deserialized.Value);
		Assert.Equal(0, deserialized.BeforeSerializeCount);
		Assert.Equal(0, deserialized.AfterSerializeCount);
		Assert.Equal(1, deserialized.BeforeDeserializeCount);
		Assert.Equal(1, deserialized.AfterDeserializeCount);
	}

	[Test]
	public void OnAfterSerialize_RunsAfterPropertiesAreRead()
	{
		OrderedCallbackObject value = new() { Name = "Ada" };
		value.Events.Clear();

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"name":"Ada"}""", json);
		Assert.Equal(["before-serialize", "read", "after-serialize"], value.Events);
	}

	[Test]
	public void OnBeforeDeserialize_RunsBeforePropertiesAreSet()
	{
		OrderedCallbackObject value = this.Serializer.Deserialize<OrderedCallbackObject>("""{"name":"Ada"}""")!;

		Assert.Equal(["before-deserialize", "set", "after-deserialize"], value.Events);
		Assert.Equal("Ada", value.Name);
	}

	[Test]
	public void OnAfterSerialize_DoesNotChangeWrittenJson()
	{
		OrderedCallbackObject value = new() { Name = "Ada" };

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"name":"Ada"}""", json);
		Assert.Equal("changed-after-write", value.Name);
	}

	[Test]
	public void InitOnlyObject_SkipsOnBeforeDeserialize()
	{
		InitCallbackObject value = this.Serializer.Deserialize<InitCallbackObject>("""{"name":"Ada","extra":true}""")!;

		Assert.Equal("Ada", value.Name);
		Assert.True(value.Extra);
		Assert.Equal(0, value.BeforeDeserializeCount);
		Assert.Equal(1, value.AfterDeserializeCount);
	}

	[Test]
	public async Task AsyncSerialize_InvokesAfterSerialize()
	{
		CallbackObject value = new() { Value = " initial " };
		Pipe pipe = new();
		await this.Serializer.SerializeAsync(pipe.Writer, value);
		await pipe.Writer.CompleteAsync();

		Assert.Equal(1, value.BeforeSerializeCount);
		Assert.Equal(1, value.AfterSerializeCount);
		Assert.Equal("""{"value":"prepared"}""", Encoding.UTF8.GetString((await pipe.Reader.ReadAsync()).Buffer.ToArray()));
	}

	[Test]
	[Arguments(0)]
	[Arguments(1024 * 1024)]
	public async Task AsyncDeserialize_InvokesBeforeAndAfterDeserialize(int maxAsyncBuffer)
	{
		JsonSerializer serializer = this.Serializer with { MaxAsyncBuffer = maxAsyncBuffer };
		PipeReader reader = PipeReader.Create(new ReadOnlySequence<byte>("""{"value":"wire"}"""u8.ToArray()));
		CallbackObject value = (await serializer.DeserializeAsync<CallbackObject>(reader))!;

		Assert.Equal("restored", value.Value);
		Assert.Equal(1, value.BeforeDeserializeCount);
		Assert.Equal(1, value.AfterDeserializeCount);
	}

	[Test]
	public void OnAfterSerializeException_IsPropagated()
	{
		CallbackException expected = new();
		LateThrowingCallbackObject value = new(expected);

		JsonSerializationException actual = Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(value));

		Assert.Same(expected, actual.InnerException);
		Assert.Equal(1, value.BeforeSerializeCount);
	}

	[Test]
	public void OnBeforeDeserializeException_IsPropagated()
	{
		CallbackException expected = new();
		DeserializeThrowingCallbackObject.ExceptionToThrow = expected;
		try
		{
			JsonSerializationException actual = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<DeserializeThrowingCallbackObject>("""{"name":"Ada"}"""));

			Assert.Same(expected, actual.InnerException);
		}
		finally
		{
			DeserializeThrowingCallbackObject.ExceptionToThrow = null;
		}
	}

	[Test]
	public void EmptyObject_AfterDeserializeRuns()
	{
		EmptyCallbackObject value = this.Serializer.Deserialize<EmptyCallbackObject>("{}")!;

		Assert.True(value.BeforeDeserializeCalled);
		Assert.True(value.AfterDeserializeCalled);
	}

	[Test]
	public void ConstructorBoundObject_AfterDeserializeRunsAfterExtensionData()
	{
		ConstructorCallbackObject value = this.Serializer.Deserialize<ConstructorCallbackObject>("""{"name":"Ada","future":42}""")!;

		Assert.Equal("Ada:True", value.ObservedState);
		Assert.Equal(0, value.BeforeDeserializeCount);
		Assert.Equal("""{"name":"Ada","future":42}""", this.Serializer.Serialize(value));
	}

	[Test]
	public void NestedObjects_ReceiveOneCallbackPerOccurrence()
	{
		CallbackObject child = new() { Value = "child" };
		CallbackContainer value = new() { First = child, Second = child };

		this.Serializer.Serialize(value);

		Assert.Equal(2, child.BeforeSerializeCount);
		Assert.Equal(2, child.AfterSerializeCount);
	}

	[Test]
	public void Null_DoesNotReceiveCallbacks()
	{
		CallbackContainer value = new();

		this.AssertRoundtrip(value, """{"first":null,"second":null}""");
	}

	[Test]
	public void CallbackException_IsPropagated()
	{
		CallbackException expected = new();
		ThrowingCallbackObject value = new(expected);

		JsonSerializationException actual = Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(value));

		Assert.Same(expected, actual.InnerException);
	}

	[GenerateShape]
	internal partial class CallbackObject : IJsonSerializationCallbacks
	{
		public string? Value { get; set; }

		internal int BeforeSerializeCount { get; private set; }

		internal int AfterSerializeCount { get; private set; }

		internal int BeforeDeserializeCount { get; private set; }

		internal int AfterDeserializeCount { get; private set; }

		public void OnBeforeSerialize()
		{
			this.BeforeSerializeCount++;
			this.Value = "prepared";
		}

		public void OnAfterSerialize() => this.AfterSerializeCount++;

		public void OnBeforeDeserialize() => this.BeforeDeserializeCount++;

		public void OnAfterDeserialize()
		{
			this.AfterDeserializeCount++;
			this.Value = "restored";
		}
	}

	[GenerateShape]
	internal partial class EmptyCallbackObject : IJsonSerializationCallbacks
	{
		internal bool BeforeDeserializeCalled { get; private set; }

		internal bool AfterDeserializeCalled { get; private set; }

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterSerialize()
		{
		}

		public void OnBeforeDeserialize() => this.BeforeDeserializeCalled = true;

		public void OnAfterDeserialize() => this.AfterDeserializeCalled = true;
	}

	[GenerateShape]
	internal partial class ConstructorCallbackObject(string name) : IJsonSerializationCallbacks
	{
		public string Name { get; } = name;

		public UnusedDataPacket? ExtensionData { get; set; }

		internal string? ObservedState { get; private set; }

		internal int BeforeDeserializeCount { get; private set; }

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterSerialize()
		{
		}

		public void OnBeforeDeserialize() => this.BeforeDeserializeCount++;

		public void OnAfterDeserialize()
			=> this.ObservedState = $"{this.Name}:{this.ExtensionData is not null}";
	}

	[GenerateShape]
	internal partial class CallbackContainer
	{
		public CallbackObject? First { get; set; }

		public CallbackObject? Second { get; set; }
	}

	[GenerateShape]
	internal partial class ThrowingCallbackObject(CallbackException exception) : IJsonSerializationCallbacks
	{
		public void OnBeforeSerialize() => throw exception;

		public void OnAfterSerialize()
		{
		}

		public void OnBeforeDeserialize()
		{
		}

		public void OnAfterDeserialize()
		{
		}
	}

	internal sealed class CallbackException : Exception;

	[GenerateShape]
	internal partial class OrderedCallbackObject : IJsonSerializationCallbacks
	{
		private string? name;

		public string? Name
		{
			get
			{
				this.Events.Add("read");
				return this.name;
			}

			set
			{
				this.Events.Add("set");
				this.name = value;
			}
		}

		internal List<string> Events { get; } = [];

		public void OnBeforeSerialize() => this.Events.Add("before-serialize");

		public void OnAfterSerialize()
		{
			this.Events.Add("after-serialize");
			this.name = "changed-after-write";
		}

		public void OnBeforeDeserialize() => this.Events.Add("before-deserialize");

		public void OnAfterDeserialize() => this.Events.Add("after-deserialize");
	}

	[GenerateShape]
	internal partial class InitCallbackObject : IJsonSerializationCallbacks
	{
		public string? Name { get; init; }

		public bool Extra { get; init; }

		internal int BeforeDeserializeCount { get; private set; }

		internal int AfterDeserializeCount { get; private set; }

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterSerialize()
		{
		}

		public void OnBeforeDeserialize() => this.BeforeDeserializeCount++;

		public void OnAfterDeserialize() => this.AfterDeserializeCount++;
	}

	[GenerateShape]
	internal partial class LateThrowingCallbackObject(CallbackException exception) : IJsonSerializationCallbacks
	{
		public string? Name { get; set; }

		internal int BeforeSerializeCount { get; private set; }

		public void OnBeforeSerialize() => this.BeforeSerializeCount++;

		public void OnAfterSerialize() => throw exception;

		public void OnBeforeDeserialize()
		{
		}

		public void OnAfterDeserialize()
		{
		}
	}

	[GenerateShape]
	internal partial class DeserializeThrowingCallbackObject : IJsonSerializationCallbacks
	{
		public string? Name { get; set; }

		internal static Exception? ExceptionToThrow { get; set; }

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterSerialize()
		{
		}

		public void OnBeforeDeserialize() => throw ExceptionToThrow!;

		public void OnAfterDeserialize()
		{
		}
	}
}
