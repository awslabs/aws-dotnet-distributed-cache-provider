// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
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
            moqCreator.Setup(x => x.CreateTableIfNotExistsAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult("foobar"));
            moqCreator.Setup(x => x.GetTTLColumnAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>()))
                .Returns(Task<string>.FromResult("blah"));
            var cache = new DynamoDBDistributedCache(moqClient.Object, moqCreator.Object, new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName"
            });
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.Throws<ArgumentNullException>(() => cache.Get(null));
            Assert.Throws<ArgumentNullException>(() => cache.Remove(null));
            Assert.Throws<ArgumentNullException>(() => cache.Set(null, Array.Empty<byte>(), new DistributedCacheEntryOptions()));
            Assert.Throws<ArgumentNullException>(() => cache.Set(" ", null, new DistributedCacheEntryOptions()));
            Assert.Throws<ArgumentNullException>(() => cache.Set(" ", Array.Empty<byte>(), null));
            Assert.Throws<ArgumentNullException>(() => cache.Refresh(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public void GetReturnsNullWhenKeyIsNotFound()
        {
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), CancellationToken.None))
                .Throws(new ResourceNotFoundException(""));
            var moqCreator = new Moq.Mock<IDynamoDBTableCreator>();
            //Mock method calls to make sure DynamoDBDistributedCache.Startup() returns immediately. 
            moqCreator.Setup(x => x.CreateTableIfNotExistsAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult("foobar"));
            moqCreator.Setup(x => x.GetTTLColumnAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>()))
                .Returns(Task<string>.FromResult("blah"));
            var cache = new DynamoDBDistributedCache(moqClient.Object, moqCreator.Object, new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName"
            });
            Assert.Null(cache.Get("foo"));
        }

        [Fact]
        public void DeleteDoesNotThrowExceptionWhenKeyIsNotFound()
        {
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), CancellationToken.None))
                .Throws(new ResourceNotFoundException(""));
            var moqCreator = new Moq.Mock<IDynamoDBTableCreator>();
            //Mock method calls to make sure DynamoDBDistributedCache.Startup() returns immediately. 
            moqCreator.Setup(x => x.CreateTableIfNotExistsAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult("foobar"));
            moqCreator.Setup(x => x.GetTTLColumnAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>()))
                .Returns(Task<string>.FromResult("blah"));
            var cache = new DynamoDBDistributedCache(moqClient.Object, moqCreator.Object, new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName"
            });
            //If this throws an exception, then the test fails
            cache.Remove("foo");
        }

        [Fact]
        public void RefreshDoesNotThrowExceptionWhenKeyIsNotFoundOnFirstGet()
        {
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(new GetItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        {
                            DynamoDBDistributedCache.TTL_WINDOW, new AttributeValue{S = new TimeSpan(1, 0, 0).ToString()}
                        },
                        {
                            DynamoDBDistributedCache.TTL_DATE, new AttributeValue{N = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds().ToString()}
                        },
                        {
                            DynamoDBDistributedCache.TTL_DEADLINE, new AttributeValue{N = DateTimeOffset.UtcNow.AddHours(3).ToUnixTimeSeconds().ToString()}
                        }
                    }
                }));
            //Client return empty UpdateItemResponse to show that DynamoDB "updated" the item. This allows the test to pass
            moqClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(new UpdateItemResponse()));
            var moqCreator = new Moq.Mock<IDynamoDBTableCreator>();
            //Mock method calls to make sure DynamoDBDistributedCache.Startup() returns immediately. 
            moqCreator.Setup(x => x.CreateTableIfNotExistsAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult("foobar"));
            moqCreator.Setup(x => x.GetTTLColumnAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>()))
                .Returns(Task<string>.FromResult("blah"));
            var cache = new DynamoDBDistributedCache(moqClient.Object, moqCreator.Object, new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName"
            });
            //Test passes if this does not throw exception
            cache.Refresh("foo");
        }

        [Fact]
        public void RefreshDoesNotThrowExceptionWhenKeyIsNotFoundOnUpdate()
        {
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(new GetItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        {
                            DynamoDBDistributedCache.TTL_WINDOW, new AttributeValue{S = new TimeSpan(1, 0, 0).ToString()}
                        },
                        {
                            DynamoDBDistributedCache.TTL_DATE, new AttributeValue{N = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds().ToString()}
                        },
                        {
                            DynamoDBDistributedCache.TTL_DEADLINE, new AttributeValue{N = DateTimeOffset.UtcNow.AddHours(3).ToUnixTimeSeconds().ToString()}
                        }
                    }
                }));
            //Client throws exception that the key trying to be updated was not found. Library should still return and not throw an excpetion
            moqClient.Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), CancellationToken.None))
                .Throws(new ResourceNotFoundException(""));
            var moqCreator = new Moq.Mock<IDynamoDBTableCreator>();
            //Mock method calls to make sure DynamoDBDistributedCache.Startup() returns immediately. 
            moqCreator.Setup(x => x.CreateTableIfNotExistsAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult("foobar"));
            moqCreator.Setup(x => x.GetTTLColumnAsync(It.IsAny<IAmazonDynamoDB>(), It.IsAny<string>()))
                .Returns(Task<string>.FromResult("blah"));
            var cache = new DynamoDBDistributedCache(moqClient.Object, moqCreator.Object, new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName"
            });
            //Test passes if this does not throw exception
            cache.Refresh("foo");
        }
    }
}
