// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal parser members are intentionally undocumented in this file.

using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Nerdbank.Json;

/// <summary>
/// Parses a strongly typed member-access expression into a <see cref="JsonPath"/> and the target type shape,
/// without compiling the expression, by walking the shape graph alongside the expression.
/// </summary>
internal static class JsonPathExpressionParser
{
	internal static (JsonPath Path, ITypeShape<TValue> TargetShape) Parse<TRoot, TValue>(
		Expression<Func<TRoot, TValue>> expression,
		ITypeShape<TRoot> rootShape,
		ConverterCache owner,
		JsonNamingPolicy? dictionaryKeyNamingPolicy)
	{
		ParameterExpression parameter = expression.Parameters[0];
		List<Access> accesses = [];
		Expression node = expression.Body;

		while (node is not ParameterExpression)
		{
			switch (node)
			{
				case MemberExpression member when member.Expression is not null:
					accesses.Add(Access.ForMember(member.Member));
					node = member.Expression;
					break;

				case BinaryExpression { NodeType: ExpressionType.ArrayIndex } arrayIndex:
					accesses.Add(Access.ForIndexOrKey(EvaluateConstant(arrayIndex.Right)));
					node = arrayIndex.Left;
					break;

				case MethodCallExpression { Method.Name: "get_Item", Object: not null } call when call.Arguments.Count == 1:
					accesses.Add(Access.ForIndexOrKey(EvaluateConstant(call.Arguments[0])));
					node = call.Object;
					break;

				case IndexExpression { Object: not null } index when index.Arguments.Count == 1:
					accesses.Add(Access.ForIndexOrKey(EvaluateConstant(index.Arguments[0])));
					node = index.Object;
					break;

				case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert:
					node = convert.Operand;
					break;

				default:
					throw new NotSupportedException($"The targeted-deserialization expression contains an unsupported node of kind '{node.NodeType}'. Only member access and constant array/dictionary indexing are supported.");
			}
		}

		if (!ReferenceEquals(node, parameter))
		{
			throw new NotSupportedException("The targeted-deserialization expression must be rooted at the lambda parameter.");
		}

		accesses.Reverse();

		JsonPath path = JsonPath.Root;
		ITypeShape current = rootShape;
		foreach (Access access in accesses)
		{
			if (access.Member is { } member)
			{
				if (current is not IObjectTypeShape objectShape)
				{
					throw new NotSupportedException($"The targeted-deserialization expression accesses member '{member.Name}' on non-object type '{current.Type.FullName}'.");
				}

				IPropertyShape property = FindProperty(objectShape, member.Name)
					?? throw new NotSupportedException($"'{current.Type.FullName}' has no serializable member named '{member.Name}'.");
				path = path.Member(owner.GetSerializedPropertyName(property.Name, property.AttributeProvider));
				current = property.PropertyType;
			}
			else
			{
				(path, current) = ApplyIndexOrKey(path, current, access.IndexOrKey, dictionaryKeyNamingPolicy);
			}
		}

		if (current.Type != typeof(TValue))
		{
			throw new NotSupportedException($"The targeted-deserialization expression resolves to '{current.Type.FullName}', which does not match the requested type '{typeof(TValue).FullName}'.");
		}

		return (path, (ITypeShape<TValue>)current);
	}

	private static (JsonPath Path, ITypeShape Current) ApplyIndexOrKey(JsonPath path, ITypeShape current, object? indexOrKey, JsonNamingPolicy? dictionaryKeyNamingPolicy)
	{
		if (current is IDictionaryTypeShape dictionary)
		{
			string key = indexOrKey switch
			{
				string s => s,
				null => throw new NotSupportedException("A null dictionary key is not supported in a targeted-deserialization expression."),
				IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
				_ => indexOrKey.ToString()!,
			};
			string serializedKey = dictionaryKeyNamingPolicy?.ConvertName(key) ?? key;
			return (path.Member(serializedKey), dictionary.ValueType);
		}

		if (current is IEnumerableTypeShape enumerable)
		{
			if (indexOrKey is not int index)
			{
				throw new NotSupportedException("Array or list indexing in a targeted-deserialization expression requires a constant integer index.");
			}

			return (path.Index(index), enumerable.ElementType);
		}

		throw new NotSupportedException($"The targeted-deserialization expression indexes into '{current.Type.FullName}', which is neither an array, list, nor dictionary.");
	}

	private static IPropertyShape? FindProperty(IObjectTypeShape objectShape, string clrName)
	{
		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (property.Name == clrName)
			{
				return property;
			}
		}

		return null;
	}

	private static object? EvaluateConstant(Expression expression) => expression switch
	{
		ConstantExpression constant => constant.Value,
		UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert => EvaluateConstant(convert.Operand),
		_ => throw new NotSupportedException("Array and dictionary indices in a targeted-deserialization expression must be compile-time constants. Use the pre-parsed JsonPath API for dynamic indices."),
	};

	private readonly struct Access
	{
		private Access(MemberInfo? member, object? indexOrKey)
		{
			this.Member = member;
			this.IndexOrKey = indexOrKey;
		}

		internal MemberInfo? Member { get; }

		internal object? IndexOrKey { get; }

		internal static Access ForMember(MemberInfo member) => new(member, null);

		internal static Access ForIndexOrKey(object? indexOrKey) => new(null, indexOrKey);
	}
}
