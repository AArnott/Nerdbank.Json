// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	private static bool ShouldWrapSerializationException(Exception exception, CancellationToken cancellationToken)
		=> exception is not JsonSerializationException && (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested);

	private static void EnsureFullyConsumed(ref JsonReader reader)
	{
		try
		{
			reader.EnsureFullyConsumed();
		}
		catch (FormatException ex)
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
	}

	private static async IAsyncEnumerable<T?> NormalizeSequenceExceptions<T>(IAsyncEnumerable<T?> sequence, CancellationToken operationCancellationToken, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		IAsyncEnumerator<T?> enumerator = sequence.GetAsyncEnumerator(cancellationToken);
		try
		{
			while (true)
			{
				T? value;
				try
				{
					if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
					{
						break;
					}

					value = enumerator.Current;
				}
				catch (Exception ex) when (ShouldWrapSerializationException(ex, operationCancellationToken) && ShouldWrapSerializationException(ex, cancellationToken))
				{
					throw new JsonSerializationException("An error occurred during deserialization.", ex);
				}

				yield return value;
			}
		}
		finally
		{
			try
			{
				await enumerator.DisposeAsync().ConfigureAwait(false);
			}
			catch (Exception ex) when (ShouldWrapSerializationException(ex, operationCancellationToken) && ShouldWrapSerializationException(ex, cancellationToken))
			{
				throw new JsonSerializationException("An error occurred during deserialization.", ex);
			}
		}
	}

	private static void ValidateSequenceOptions(SequenceOptions options)
	{
		if (options.Format is not (JsonSequenceFormat.NDJson or JsonSequenceFormat.JsonArray))
		{
			throw new ArgumentOutOfRangeException(nameof(options), options.Format, "Unsupported JSON sequence format.");
		}
	}
}
