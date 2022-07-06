using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime.Internal;
using AWS.DistributedCacheProvider;
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
            var creator = new DynamoDBTableCreator(GetSleepMocker().Object);
            //create table, set create boolean to false.
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateIfNotExistsAsync(moqClient.Object, "", false, false, ""));
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
                        TableStatus = "Active"
                    }
                }));
            //mock create table that it returns immediately 
            moqClient.Setup(x => x.CreateTableAsync(It.IsAny<CreateTableRequest>(), It.IsAny<CancellationToken>()));
            var creator = new DynamoDBTableCreator(GetSleepMocker().Object);
            //create table, set create boolean to true.
            await creator.CreateIfNotExistsAsync(moqClient.Object, "", true, false, "");
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
                                KeyType = "HASH"
                            }
                        },
                        //And is of type String
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition
                            {
                                AttributeName = keyName,
                                AttributeType = "S"
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator(GetSleepMocker().Object);
            await creator.CreateIfNotExistsAsync(moqClient.Object, "", false, false, "");
        }

        /// <summary>
        /// Describe table returns an existing table. Table is invalid to be used for a cache becuase it has a composite key. Exception
        /// </summary>
        [Fact]
        public void TableExists_TooManyKeys_Invalid()
        {
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
                                AttributeName = "key",
                                KeyType = "HASH"
                            },
                            new KeySchemaElement
                            {
                                AttributeName = "key2",
                                KeyType = "HASH"
                            }
                        },
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition
                            {
                                AttributeName = "key",
                                AttributeType = "S"
                            },
                            new AttributeDefinition
                            {
                                AttributeName = "key2",
                                AttributeType = "S"
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator(GetSleepMocker().Object);
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateIfNotExistsAsync(moqClient.Object, "", false, false, ""));
        }

        /// <summary>
        /// Describe table returns an existing table. Table is invalid to be used for a cache becuase it has a bad key attribute type. Exception
        /// </summary>
        [Fact]
        public void TableExists_BadKeyAttributeType_Invalid()
        {
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
                                AttributeName = "key",
                                KeyType = "HASH"
                            }
                        },
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            //But is of type Number
                            new AttributeDefinition
                            {
                                AttributeName = "key",
                                AttributeType = "N"
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator(GetSleepMocker().Object);
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateIfNotExistsAsync(moqClient.Object, "", false, false, ""));
        }

        /// <summary>
        /// Describe table returns an existing table. Table is invalid to be used for a cache becuase it has a bad key type. Exception
        /// </summary>
        [Fact]
        public void TableExists_BadKeyType_Invalid()
        {
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
                                AttributeName = "key",
                                KeyType = "RANGE"
                            }
                        },
                        AttributeDefinitions = new List<AttributeDefinition>
                        {
                            new AttributeDefinition
                            {
                                AttributeName = "key",
                                AttributeType = "S"
                            }
                        }
                    }
                }));
            var creator = new DynamoDBTableCreator(GetSleepMocker().Object);
            Assert.ThrowsAsync<AmazonDynamoDBException>(() => creator.CreateIfNotExistsAsync(moqClient.Object, "", false, false, ""));
        }

        /// <summary>
        /// Creates a Mock object of the IThreadSleeper class that immediatly returns when the IThreadSleeper.Sleep() method is called
        /// </summary>
        /// <returns>The Moq IThreadSleeper object</returns>
        private Mock<IThreadSleeper> GetSleepMocker()
        {
            var sleepMoq = new Moq.Mock<IThreadSleeper>();
            sleepMoq.Setup(x => x.Sleep(It.IsAny<int>()));
            return sleepMoq;
        }
    }
}
