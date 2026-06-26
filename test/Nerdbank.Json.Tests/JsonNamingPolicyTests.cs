// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;
using Xunit;

/// <summary>
/// Verifies property name transformation behaviors of <see cref="JsonNamingPolicy"/>.
/// </summary>
public class JsonNamingPolicyTests
{
	[Theory]
	[InlineData("urlValue", "URLValue")]
	[InlineData("person", "Person")]
	[InlineData("xml2Json", "Xml2Json")]
	[InlineData("isJsonProperty", "IsJSONProperty")]
	[InlineData("😀葛🀄", "😀葛🀄")]
	[InlineData("", "")]
	public void CamelCaseNamingPolicy(string expected, string input)
	{
		string actual = JsonNamingPolicy.CamelCase.ConvertName(input);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData("PropertyName", "propertyName")]
	[InlineData("UrlValue", "urlValue")]
	[InlineData("IPhone", "iPhone")]
	[InlineData("", " ")]
	public void PascalCaseNamingPolicy(string expected, string input)
	{
		string actual = JsonNamingPolicy.PascalCase.ConvertName(input);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void CamelCaseNullNameThrows()
	{
		Assert.Throws<ArgumentNullException>(() => JsonNamingPolicy.CamelCase.ConvertName(null!));
	}
}
