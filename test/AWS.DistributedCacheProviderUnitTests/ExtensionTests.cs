// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AWS.DistributedCacheProviderUnitTests
{
    public  class ExtensionTests
    {
        /// <summary>
        /// Tests that our extension method returns a DynamoDBDistributedCache
        /// </summary>
        [Fact]
        public void TestExtensionMethodsToReturnValidCache()
        {
            var serviceContainer = new ServiceCollection();
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            serviceContainer.AddSingleton<IAmazonDynamoDB>(moqClient.Object);
            serviceContainer.AddAWSDynamoDBDistributedCache(options =>
            {
                options.TableName = "blah";
                options.CreateTableIfNotExists = false;
            });
            var provider = ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(serviceContainer);
            var cache = provider.GetService<IDistributedCache>();
            Assert.NotNull(cache);
        }
    }
}
