// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.DistributedCacheProvider;
using AWS.DistributedCacheProvider.Internal;
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
                            DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME, new AttributeValue{N = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds().ToString()}
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
                            DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME, new AttributeValue{N = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds().ToString()}
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

        [Fact]
        public void WorkInLeastPrivilegeMode()
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
                            DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME, new AttributeValue{N = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds().ToString()}
                        },
                        {
                            DynamoDBDistributedCache.TTL_DEADLINE, new AttributeValue{N = DateTimeOffset.UtcNow.AddHours(3).ToUnixTimeSeconds().ToString()}
                        }
                    }
                }));

            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), CancellationToken.None))
                .Returns((DescribeTableRequest r, CancellationToken token) =>
                {
                    throw new Exception("DescribeTableAsync should not be called");
                });

            moqClient.Setup(x => x.DescribeTimeToLiveAsync(It.IsAny<DescribeTimeToLiveRequest>(), CancellationToken.None))
                .Returns((DescribeTableRequest r, CancellationToken token) =>
                {
                    throw new Exception("DescribeTimeToLiveAsync should not be called");
                });

            moqClient.Setup(x => x.CreateTableAsync(It.IsAny<CreateTableRequest>(), CancellationToken.None))
                .Returns((CreateTableRequest r, CancellationToken token) =>
                {
                    throw new Exception("CreateTableAsync should not be called");
                });

            moqClient.Setup(x => x.UpdateTimeToLiveAsync(It.IsAny<UpdateTimeToLiveRequest>(), CancellationToken.None))
                .Returns((UpdateTimeToLiveRequest r, CancellationToken token) =>
                {
                    throw new Exception("DescribeTimeToLiveAsync should not be called");
                });

            var options = new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName",
                PartitionKeyName = "foo_id",
                TTLAttributeName = "bar_date"
            };

            var cache = new DynamoDBDistributedCache(moqClient.Object, new DynamoDBTableCreator(), options);

            cache.Get("foo");
        }

        [Fact]
        public void CheckExistingTableAttributesWithTTL()
        {
            const string partitionKeyName = "myId";
            const string ttlName = "myTtl";

            var moqClient = new Moq.Mock<IAmazonDynamoDB>();

            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(new DescribeTableResponse
                {
                    Table = new TableDescription
                    {
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement()
                            {
                                AttributeName = partitionKeyName,
                                KeyType = KeyType.HASH
                            }
                        }
                    }
                }));

            moqClient.Setup(x => x.DescribeTimeToLiveAsync(It.IsAny<DescribeTimeToLiveRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(new DescribeTimeToLiveResponse
                {
                    TimeToLiveDescription = new TimeToLiveDescription
                    {
                        AttributeName = ttlName,
                        TimeToLiveStatus = TimeToLiveStatus.ENABLED
                    }
                }));

            moqClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), CancellationToken.None))
                .Callback<GetItemRequest, CancellationToken>((request, token) =>
                {
                    Assert.True(request.Key.ContainsKey(partitionKeyName));
                })
                .Returns(Task.FromResult(new GetItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        {
                            DynamoDBDistributedCache.TTL_WINDOW, new AttributeValue{S = new TimeSpan(1, 0, 0).ToString()}
                        },
                        {
                            ttlName, new AttributeValue{N = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds().ToString()}
                        },
                        {
                            DynamoDBDistributedCache.TTL_DEADLINE, new AttributeValue{N = DateTimeOffset.UtcNow.AddHours(3).ToUnixTimeSeconds().ToString()}
                        }
                    }
                }));

            moqClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), CancellationToken.None))
                .Callback<PutItemRequest, CancellationToken>((request, token) =>
                {
                    Assert.True(request.Item.ContainsKey(partitionKeyName));
                    Assert.True(request.Item.ContainsKey(ttlName));
                })
                .Returns(Task.FromResult(new PutItemResponse
                {
                    
                }));


            var options = new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName"
            };

            var cache = new DynamoDBDistributedCache(moqClient.Object, new DynamoDBTableCreator(), options);

            cache.Get("foo");

            cache.SetString("foo", "bar", new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1)
            });
        }

        [Fact]
        public void CheckExistingTableAttributesWithoutTTL()
        {
            const string partitionKeyName = "myId";

            var moqClient = new Moq.Mock<IAmazonDynamoDB>();

            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(new DescribeTableResponse
                {
                    Table = new TableDescription
                    {
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement()
                            {
                                AttributeName = partitionKeyName,
                                KeyType = KeyType.HASH
                            }
                        }
                    }
                }));

            moqClient.Setup(x => x.DescribeTimeToLiveAsync(It.IsAny<DescribeTimeToLiveRequest>(), CancellationToken.None))
                .Returns(Task.FromResult(new DescribeTimeToLiveResponse
                {
                    TimeToLiveDescription = new TimeToLiveDescription
                    {
                        TimeToLiveStatus = TimeToLiveStatus.DISABLED
                    }
                }));

            moqClient.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), CancellationToken.None))
                .Callback<GetItemRequest, CancellationToken>((request, token) =>
                {
                    Assert.True(request.Key.ContainsKey(partitionKeyName));
                })
                .Returns(Task.FromResult(new GetItemResponse
                {
                    Item = new Dictionary<string, AttributeValue>
                    {
                        {
                            DynamoDBDistributedCache.TTL_WINDOW, new AttributeValue{S = new TimeSpan(1, 0, 0).ToString()}
                        },
                        {
                            DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME, new AttributeValue{N = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds().ToString()}
                        },
                        {
                            DynamoDBDistributedCache.TTL_DEADLINE, new AttributeValue{N = DateTimeOffset.UtcNow.AddHours(3).ToUnixTimeSeconds().ToString()}
                        }
                    }
                }));

            moqClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), CancellationToken.None))
                .Callback<PutItemRequest, CancellationToken>((request, token) =>
                {
                    Assert.True(request.Item.ContainsKey(partitionKeyName));
                    Assert.True(request.Item.ContainsKey(DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME));
                })
                .Returns(Task.FromResult(new PutItemResponse
                {

                }));


            var options = new DynamoDBDistributedCacheOptions
            {
                TableName = "MyTableName"
            };

            var cache = new DynamoDBDistributedCache(moqClient.Object, new DynamoDBTableCreator(), options);

            cache.Get("foo");

            cache.SetString("foo", "bar", new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1)
            });
        }
    }
}
