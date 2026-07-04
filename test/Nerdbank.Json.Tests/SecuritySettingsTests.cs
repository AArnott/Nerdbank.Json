// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SecuritySettingsTests
{
	[Test]
	public void DefaultCtorMatchesUntrustedData()
	{
		Assert.Equal(SecuritySettings.UntrustedData, new SecuritySettings());
	}

	[Test]
	public void TrustedDataExists()
	{
		Assert.NotNull(SecuritySettings.TrustedData);
	}
}
