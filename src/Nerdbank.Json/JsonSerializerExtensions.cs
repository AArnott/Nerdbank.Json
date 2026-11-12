// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Extension methods for the <see cref="JsonSerializer"/> class.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods preserve the convenient serializer entry points for target frameworks
/// that cannot use the constrained <c>IShapeable&lt;T&gt;</c> instance overloads, and for values that
/// rely on dynamic shape discovery or witness types.
/// </para>
/// <para>
/// Some of these methods will be very typical when targeting .NET Standard or .NET Framework,
/// but when targeting .NET, the compiler will prefer the instance methods on the
/// <see cref="JsonSerializer"/> class itself.
/// These instance methods are faster and produce compile errors rather than runtime exceptions
/// that may be thrown by these extension methods.
/// </para>
/// </remarks>
public static partial class JsonSerializerExtensions
{
	private static JsonSerializer RequireSerializer(JsonSerializer? serializer)
		=> serializer ?? throw new ArgumentNullException(nameof(serializer));

	private static ITypeShape<T>? TryResolveTypeShape<T>(ConverterCache? cache)
	{
		try
		{
			return ResolveTypeShapeOrThrow<T>(cache);
		}
		catch (NotSupportedException)
		{
			return null;
		}
	}

	private static ITypeShape<T> ResolveTypeShapeOrThrow<T>(ConverterCache? cache)
	{
		try
		{
			return cache is null ? TypeShapeResolver.ResolveDynamicOrThrow<T>() : cache.ResolveDynamicTypeShapeOrThrow<T>();
		}
		catch (NotSupportedException ex) when (typeof(T).IsArray)
		{
			throw new NotSupportedException(
				$"The type '{typeof(T).FullName}' does not have a generated shape. " +
				$"To serialize or deserialize an array as a top-level type, use a witness type with the [GenerateShapeFor<{typeof(T).Name}>] attribute. " +
				$"For example:\n\n" +
				$"[GenerateShapeFor<{typeof(T).Name}>]\n" +
				$"partial class Witness;\n\n" +
				$"var result = serializer.Deserialize<{typeof(T).Name}, Witness>(json);",
				ex);
		}
	}

	private static ITypeShape<T> ResolveTypeShapeOrThrow<T, TProvider>(ConverterCache cache)
	{
		try
		{
			return cache.ResolveDynamicTypeShapeOrThrow<T, TProvider>();
		}
		catch (NotSupportedException ex) when (typeof(T).IsArray)
		{
			throw new NotSupportedException(
				$"The type '{typeof(T).FullName}' does not have a generated shape on the witness type '{typeof(TProvider).FullName}'. " +
				$"To serialize or deserialize an array as a top-level type, ensure the witness type has a [GenerateShapeFor<{typeof(T).Name}>] attribute. " +
				$"For example:\n\n" +
				$"[GenerateShapeFor<{typeof(T).Name}>]\n" +
				$"partial class {typeof(TProvider).Name};\n\n" +
				$"Then use the witness type when serializing or deserializing:\n" +
				$"var result = serializer.Deserialize<{typeof(T).Name}, {typeof(TProvider).Name}>(json);",
				ex);
		}
	}
}
