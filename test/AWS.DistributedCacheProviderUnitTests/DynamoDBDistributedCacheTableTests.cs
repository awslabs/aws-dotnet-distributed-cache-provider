using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.DistributedCacheProvider.Internal;
using Moq;
using Xunit;

namespace AWS.DistributedCacheProviderUnitTests
{
    /// <summary>
    /// The purpose of this test class is to test the DynamoDBTableCreator class.
    /// </summary>
    public class DynamoDBDistributedCacheTableTests
    {
        /// <summary>
        /// Describe table throws a ResourceNotFoundException, and the create table boolean is turned off. Expect Exception
        /// </summary>
        [Fact]
        public void CreateIfNotExists_TableDoesNotExist_DoNotCreate_ExpectException()
        {
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            //mock describe table that table does not exist
            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
                .Throws(new ResourceNotFoundException(""));
            var creator = new DynamoDBTableCreator();
            //create table, set create boolean to false.
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateTableIfNotExistsAsync(moqClient.Object, "", false, "", ""));
        }

        /// <summary>
        /// Describe table throws a ResourceNotFoundException, and the create table is turned on. Do not expect Exception
        /// </summary>
        [Fact]
        public async void CreateIfNotExists_TableDoesNotExist_Create_NoException()
        {
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            //mock describe table that table does not exist. Then the next time return an active table
            moqClient.SetupSequence(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
                .Throws(new ResourceNotFoundException(""))
                .Returns(Task.FromResult(new DescribeTableResponse
                {
                    Table = new TableDescription
                    {
                        TableStatus = TableStatus.ACTIVE
                    }
                }));
            //mock create table that it returns immediately 
            moqClient.Setup(x => x.CreateTableAsync(It.IsAny<CreateTableRequest>(), It.IsAny<CancellationToken>()));
            var creator = new DynamoDBTableCreator();
            //create table, set create boolean to true.
            await creator.CreateTableIfNotExistsAsync(moqClient.Object, "", true, "", "");
        }

        /// <summary>
        /// Describe table returns an existing table. Table is valid to be used for a cache. No exception
        /// </summary>
        [Fact]
        public async void TableExists_Valid()
        {
            var keyName = "key";
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new DescribeTableResponse
                {
                    Table = new TableDescription
                    {
                        //Key is a non-composite Hash key
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement
                            {
                                AttributeName = keyName,
                                KeyType = KeyType.HASH
                            }
                        },
                        //And is of type String
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition
                            {
                                AttributeName = keyName,
                                AttributeType = ScalarAttributeType.S
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator();
            await creator.CreateTableIfNotExistsAsync(moqClient.Object, "", false, "", "");
        }

        /// <summary>
        /// Describe table returns an existing table. Table is invalid to be used for a cache becuase it has a composite key. Exception
        /// </summary>
        [Fact]
        public void TableExists_TooManyKeys_Invalid()
        {
            var key1 = "key";
            var key2 = "key2";
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new DescribeTableResponse
                {
                    Table = new TableDescription
                    {
                        //Key is not a non-compisite Key
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement
                            {
                                AttributeName = key1,
                                KeyType = KeyType.HASH
                            },
                            new KeySchemaElement
                            {
                                AttributeName = key2,
                                KeyType = KeyType.HASH
                            }
                        },
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition
                            {
                                AttributeName = key1,
                                AttributeType = ScalarAttributeType.S
                            },
                            new AttributeDefinition
                            {
                                AttributeName = key2,
                                AttributeType = ScalarAttributeType.S
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator();
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateTableIfNotExistsAsync(moqClient.Object, "", false, "", ""));
        }

        /// <summary>
        /// Describe table returns an existing table. Table is invalid to be used for a cache becuase it has a bad key attribute type. Exception
        /// </summary>
        [Fact]
        public void TableExists_BadKeyAttributeType_Invalid()
        {
            var key = "key";
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new DescribeTableResponse
                {
                    Table = new TableDescription
                    {
                        KeySchema = new List<KeySchemaElement>
                        {
                            //Key is a non-composite Hash key
                            new KeySchemaElement
                            {
                                AttributeName = key,
                                KeyType = KeyType.HASH
                            }
                        },
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            //But is of type Number
                            new AttributeDefinition
                            {
                                AttributeName = key,
                                AttributeType = ScalarAttributeType.N
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator();
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateTableIfNotExistsAsync(moqClient.Object, "", false, "", ""));
        }

        /// <summary>
        /// Describe table returns an existing table. Table is invalid to be used for a cache becuase it has a bad key type. Exception
        /// </summary>
        [Fact]
        public void TableExists_BadKeyType_Invalid()
        {
            var key = "key";
            var moqClient = new Moq.Mock<IAmazonDynamoDB>();
            moqClient.Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new DescribeTableResponse
                {
                    Table = new TableDescription
                    {
                        //Key is non-composite. But is a Range key
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement
                            {
                                AttributeName = key,
                                KeyType = KeyType.RANGE
                            }
                        },
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition
                            {
                                AttributeName = key,
                                AttributeType = ScalarAttributeType.S
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator();
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateTableIfNotExistsAsync(moqClient.Object, "", false, "", ""));
        }
    }
}
