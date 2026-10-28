// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType.Abstractions;
using PolyType.SourceGenerator;
using PolyType.SourceGenModel;

public partial class ConstructorArgumentStateTests : TestBase
{
	public enum ReadOutcome
	{
		Success,
		InvalidData,
		MissingRequiredArgument,
		ThrowingConstructor,
		Cancellation,
	}

	[Test, MatrixDataSource]
	public async Task Read_ReturnsArgumentState(bool asynchronous, ReadOutcome outcome)
	{
		TrackingArgumentState state = new();
		InvalidOperationException constructorException = new("Constructor failed.");
		using CancellationTokenSource cancellationSource = new();
		IObjectTypeShape<Model> originalShape = (IObjectTypeShape<Model>)TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Model>()!;
		SourceGenObjectTypeShape<Model> shape = new()
		{
			Provider = originalShape.Provider,
			IsContextual = true,
			PropertiesFactory = () => originalShape.Properties,
			ConstructorFactory = () => new SourceGenConstructorShape<Model, TrackingArgumentState>
			{
				DeclaringType = originalShape,
				IsPublic = true,
				ArgumentStateConstructor = () => state,
				ParameterizedConstructor = Construct,
				ParametersFactory = () =>
				[
					new SourceGenParameterShape<TrackingArgumentState, int>
					{
						Position = 0,
						Name = nameof(Model.Value),
						Kind = ParameterKind.MethodParameter,
						IsRequired = true,
						IsPublic = true,
						ParameterType = ((IPropertyShape<Model, int>)originalShape.Properties[0]).PropertyType,
						Getter = (ref TrackingArgumentState argumentState) => argumentState.Value,
						Setter = (ref TrackingArgumentState argumentState, int value) => argumentState.Value = value,
					},
				],
			},
		};
		string json = outcome switch
		{
			ReadOutcome.InvalidData => """{"value":?}""",
			ReadOutcome.MissingRequiredArgument => "{}",
			_ => """{"value":3}""",
		};

		switch (outcome)
		{
			case ReadOutcome.Success:
				Assert.Equal(new Model(3), await Read());
				break;
			case ReadOutcome.InvalidData:
			case ReadOutcome.MissingRequiredArgument:
				await Assert.ThrowsAsync<JsonSerializationException>(async () => await Read());
				break;
			case ReadOutcome.ThrowingConstructor:
				JsonSerializationException error = await Assert.ThrowsAsync<JsonSerializationException>(async () => await Read());
				Assert.Same(constructorException, error.GetBaseException());
				break;
			case ReadOutcome.Cancellation:
				await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Read());
				break;
		}

		Assert.Equal(1, state.ReturnCount);

		Model Construct(ref TrackingArgumentState argumentState)
		{
			Assert.Same(state, argumentState);
			Assert.Equal(0, state.ReturnCount);
			if (outcome == ReadOutcome.ThrowingConstructor)
			{
				throw constructorException;
			}

			if (outcome == ReadOutcome.Cancellation)
			{
				cancellationSource.Cancel();
				cancellationSource.Token.ThrowIfCancellationRequested();
			}

			return new(argumentState.Value);
		}

		async ValueTask<Model?> Read()
		{
			if (asynchronous)
			{
				using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));
				return await this.Serializer.DeserializeAsync(stream, shape, cancellationSource.Token);
			}

			return this.Serializer.Deserialize(json, shape, cancellationSource.Token);
		}
	}

	[GenerateShape]
	public partial record Model(int Value);

#pragma warning disable PT0019 // Test double for verifying the argument-state lifetime contract.
	private class TrackingArgumentState : IArgumentState
#pragma warning restore PT0019
	{
		public int Count => 1;

		public bool AreRequiredArgumentsSet => true;

		public int Value { get; set; }

		public int ReturnCount { get; private set; }

		public bool IsArgumentSet(int index) => true;

		public void Return() => this.ReturnCount++;
	}
}
