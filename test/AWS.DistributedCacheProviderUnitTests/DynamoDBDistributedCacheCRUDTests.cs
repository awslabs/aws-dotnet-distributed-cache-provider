// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using AWS.DistributedCacheProvider;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace AWS.DistributedCacheProviderUnitTests
{
    public class DynamoDBDistributedCacheCRUDTests
    {
        [Fact]
        public void NullParameterExceptions()
        {
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            var moqCreator = new Moq.Mock<IDynamoDBTableCreator>();
            //Mock method calls to make sure DynamoDBDistributedCache.Startup() returns immediately. 
            moqCreator.Setup(x => x.CreateTableIfNotExistsAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            moqCreator.Setup(x => x.GetTTLColumnAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>()))
                .Returns(Task<string>.FromResult("blah"));
            var cache = new DynamoDBDistributedCache(moqClient.Object, moqCreator.Object, new DynamoDBDistributedCacheOptions());
            Assert.Throws<ArgumentNullException>(() => cache.Get(null));
            Assert.Throws<ArgumentNullException>(() => cache.Remove(null));
            Assert.Throws<ArgumentNullException>(() => cache.Set(null, new byte[0], new DistributedCacheEntryOptions()));
            Assert.Throws<ArgumentNullException>(() => cache.Set(" ", null, new DistributedCacheEntryOptions()));
            Assert.Throws<ArgumentNullException>(() => cache.Set(" ", new byte[0], null));
            Assert.Throws<ArgumentNullException>(() => cache.Refresh(null));
        }
    }
}
