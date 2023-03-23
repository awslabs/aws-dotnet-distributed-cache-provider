// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using AWS.DistributedCacheProvider.Internal;
using Xunit;

namespace AWS.DistributedCacheProviderUnitTests
{
    public class UtilitiesTests
    {
        [Theory]
        [InlineData("foo", null, "dc:foo")]
        [InlineData("foo", "bar", "bar:dc:foo")]
        public void FormatKey(string key, string? prefix, string expectedValue)
        {
            var formattedKey = Utilities.FormatPartitionKey(key, prefix);
            Assert.Equal(expectedValue, formattedKey);
        }
    }
}
