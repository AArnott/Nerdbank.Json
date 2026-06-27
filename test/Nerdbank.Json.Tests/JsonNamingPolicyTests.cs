// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;

/// <summary>
/// Verifies property name transformation behaviors of <see cref="JsonNamingPolicy"/>.
/// </summary>
public class JsonNamingPolicyTests
{
	[Test]
	public void CamelCaseNamingPolicy_ConvertsUrlValue()
		=> AssertCamelCase("urlValue", "URLValue");

	[Test]
	public void CamelCaseNamingPolicy_ConvertsPerson()
		=> AssertCamelCase("person", "Person");

	[Test]
	public void CamelCaseNamingPolicy_ConvertsXml2Json()
		=> AssertCamelCase("xml2Json", "Xml2Json");

	[Test]
	public void CamelCaseNamingPolicy_ConvertsIsJsonProperty()
		=> AssertCamelCase("isJsonProperty", "IsJSONProperty");

	[Test]
	public void CamelCaseNamingPolicy_LeavesEmojiUntouched()
		=> AssertCamelCase("😀葛🀄", "😀葛🀄");

	[Test]
	public void CamelCaseNamingPolicy_LeavesEmptyStringUntouched()
		=> AssertCamelCase(string.Empty, string.Empty);

	[Test]
	public void PascalCaseNamingPolicy_ConvertsPropertyName()
		=> AssertPascalCase("PropertyName", "propertyName");

	[Test]
	public void PascalCaseNamingPolicy_ConvertsUrlValue()
		=> AssertPascalCase("UrlValue", "urlValue");

	[Test]
	public void PascalCaseNamingPolicy_ConvertsIPhone()
		=> AssertPascalCase("IPhone", "iPhone");

	[Test]
	public void PascalCaseNamingPolicy_ConvertsSpaceToEmptyString()
		=> AssertPascalCase(string.Empty, " ");

	[Test]
	public void CamelCaseNullNameThrows()
	{
		Assert.Throws<ArgumentNullException>(() => JsonNamingPolicy.CamelCase.ConvertName(null!));
	}

	private static void AssertCamelCase(string expected, string input)
	{
		string actual = JsonNamingPolicy.CamelCase.ConvertName(input);
		Assert.Equal(expected, actual);
	}

	private static void AssertPascalCase(string expected, string input)
	{
		string actual = JsonNamingPolicy.PascalCase.ConvertName(input);
		Assert.Equal(expected, actual);
	}
}
