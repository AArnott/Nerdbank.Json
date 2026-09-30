// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;

public partial class JsonObjectSerializerTests
{
	[Test]
	public void Serialize_Delegate_ThrowsJsonSerializationException()
	{
		JsonSerializer serializer = new();
		FunctionContainer value = new() { Callback = static () => { } };

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => serializer.Serialize(value));
		Assert.IsType<NotSupportedException>(exception.InnerException);
		Assert.Contains("delegate", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void Deserialize_Delegate_ThrowsJsonSerializationException()
	{
		JsonSerializer serializer = new();

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize<FunctionContainer>("{}"));
		Assert.IsType<NotSupportedException>(exception.InnerException);
	}

	[Test]
	public void SerializeObject_Delegate_ThrowsJsonSerializationException()
	{
		JsonSerializer serializer = new();
		FunctionContainer value = new() { Callback = static () => { } };
		ITypeShape<FunctionContainer> shape = GetFunctionContainerShape();

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => serializer.SerializeObject(value, shape));
		Assert.IsType<NotSupportedException>(exception.InnerException);
	}

	[Test]
	public void DeserializeObject_Delegate_ThrowsJsonSerializationException()
	{
		JsonSerializer serializer = new();
		ITypeShape<FunctionContainer> shape = GetFunctionContainerShape();

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => serializer.DeserializeObject("{}", shape));
		Assert.IsType<NotSupportedException>(exception.InnerException);
	}

	[Test]
	public void Serialize_RefWriter_ThrowsJsonSerializationException()
	{
		JsonSerializer serializer = new();
		FunctionContainer value = new() { Callback = static () => { } };
		ITypeShape<FunctionContainer> shape = GetFunctionContainerShape();

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => SerializeWithWriter(serializer, value, shape));
		Assert.IsType<NotSupportedException>(exception.InnerException);
	}

	[Test]
	public void Deserialize_RefReader_ThrowsJsonSerializationException()
	{
		JsonSerializer serializer = new();
		ITypeShape<FunctionContainer> shape = GetFunctionContainerShape();

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => DeserializeWithReader(serializer, shape));
		Assert.IsType<NotSupportedException>(exception.InnerException);
	}

	private static void SerializeWithWriter(JsonSerializer serializer, FunctionContainer value, ITypeShape<FunctionContainer> shape)
	{
		Pipe buffer = new();
		JsonWriter writer = new(buffer.Writer);
		serializer.Serialize(ref writer, value, shape);
	}

	private static void DeserializeWithReader(JsonSerializer serializer, ITypeShape<FunctionContainer> shape)
	{
		JsonReader reader = new(System.Text.Encoding.UTF8.GetBytes("{}"));
		serializer.Deserialize(ref reader, shape);
	}

	private static ITypeShape<FunctionContainer> GetFunctionContainerShape()
		=> PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<FunctionContainer>()!;

	[GenerateShape]
	internal partial class FunctionContainer
	{
		public Action? Callback { get; set; }
	}
}
