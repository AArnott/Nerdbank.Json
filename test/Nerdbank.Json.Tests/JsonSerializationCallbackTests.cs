// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
		Assert.Equal(0, value.AfterDeserializeCount);
		Assert.Equal("restored", deserialized.Value);
		Assert.Equal(0, deserialized.BeforeSerializeCount);
		Assert.Equal(1, deserialized.AfterDeserializeCount);
	}

	[Test]
	public void EmptyObject_AfterDeserializeRuns()
	{
		EmptyCallbackObject value = this.Serializer.Deserialize<EmptyCallbackObject>("{}")!;

		Assert.True(value.AfterDeserializeCalled);
	}

	[Test]
	public void ConstructorBoundObject_AfterDeserializeRunsAfterExtensionData()
	{
		ConstructorCallbackObject value = this.Serializer.Deserialize<ConstructorCallbackObject>("""{"name":"Ada","future":42}""")!;

		Assert.Equal("Ada:future", value.ObservedState);
	}

	[Test]
	public void NestedObjects_ReceiveOneCallbackPerOccurrence()
	{
		CallbackObject child = new() { Value = "child" };
		CallbackContainer value = new() { First = child, Second = child };

		this.Serializer.Serialize(value);

		Assert.Equal(2, child.BeforeSerializeCount);
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

		CallbackException actual = Assert.Throws<CallbackException>(() => this.Serializer.Serialize(value));

		Assert.Same(expected, actual);
	}

	[GenerateShape]
	internal partial class CallbackObject : IJsonSerializationCallbacks
	{
		public string? Value { get; set; }

		internal int BeforeSerializeCount { get; private set; }

		internal int AfterDeserializeCount { get; private set; }

		public void OnBeforeSerialize()
		{
			this.BeforeSerializeCount++;
			this.Value = "prepared";
		}

		public void OnAfterDeserialize()
		{
			this.AfterDeserializeCount++;
			this.Value = "restored";
		}
	}

	[GenerateShape]
	internal partial class EmptyCallbackObject : IJsonSerializationCallbacks
	{
		internal bool AfterDeserializeCalled { get; private set; }

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize() => this.AfterDeserializeCalled = true;
	}

	[GenerateShape]
	internal partial class ConstructorCallbackObject(string name) : IJsonSerializationCallbacks
	{
		public string Name { get; } = name;

		[JsonExtensionData]
		public Dictionary<string, string>? ExtensionData { get; set; }

		internal string? ObservedState { get; private set; }

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize()
			=> this.ObservedState = $"{this.Name}:{(this.ExtensionData is { } extensionData ? string.Join(",", extensionData.Keys) : string.Empty)}";
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

		public void OnAfterDeserialize()
		{
		}
	}

	internal sealed class CallbackException : Exception;
}
