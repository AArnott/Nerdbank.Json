// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class JsonReaderTests
{
	[Test]
	public void CreatePeekReader_ReturnsIndependentCopyAtCurrentPosition()
	{
		JsonReader reader = new("[1,2]"u8);
		reader.ReadStartArray();
		Assert.Equal("1", reader.ReadNumberToken());
		reader.ReadValueSeparator();

		JsonReader peekReader = reader.CreatePeekReader();

		Assert.Equal("2", peekReader.ReadNumberToken());
		Assert.Equal("2", reader.ReadNumberToken());
		peekReader.ReadEndArray();
		reader.ReadEndArray();
	}
}
