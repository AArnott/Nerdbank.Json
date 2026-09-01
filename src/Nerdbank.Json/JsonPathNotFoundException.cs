// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// The exception thrown when a targeted deserialization path selects a value that is not present in the document
/// and <see cref="MissingPathBehavior.Throw"/> is in effect.
/// </summary>
public class JsonPathNotFoundException : JsonSerializationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonPathNotFoundException"/> class.
	/// </summary>
	/// <param name="path">The path that could not be resolved.</param>
	public JsonPathNotFoundException(string path)
		: base($"The targeted deserialization path '{path}' was not found in the JSON document.")
	{
		this.Path = path;
	}

	/// <summary>
	/// Gets the path that could not be resolved.
	/// </summary>
	public string Path { get; }
}
