using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.DistributedCacheProvider;
using Microsoft.Extensions.Options;
using Xunit;
namespace AWS.DistributedCacheProviderIntegrationTests
{
    public class DynamoDBDistributedCacheTableTests
    {
        /// <summary>
        /// Tests that our factory will create a Table if it does not already exist.
        /// </summary>
        [Fact]
        public async void CreateTable()
        {
            var tableName = "table_test_0";
            var client = new AmazonDynamoDBClient();
            //First verify that a table with this name does not already exist.
            try
            {
                _ = await client.DescribeTableAsync(tableName);
                //If no exception was thrown, then the table already exists, bad state for test.
                Assert.True(false);
            }
            catch (ResourceNotFoundException) {}
            DynamoDBDistributedCacheFactory factory = GetFactory();
            //Use factory to get our cache.
            var cache = factory.Build(Options.Create<DynamoDBDistributedCacheOptions>(new DynamoDBDistributedCacheOptions
            {
                TableName = tableName,
                CreateTableIfNotExists = true
            }));
            //With lazy implementation, table creation is delayed until the client actually needs it.
            //resolving the table should pass. We then have a NotImplementedException until we implement CRUD operations.
            Assert.Throws<NotImplementedException>(() => cache.Get(""));
            //Delete DynamoDB table
            CleanupTable(client, tableName);
        }

        /// <summary>
        /// Test that our Cache can load a table that already exists
        /// </summary>
        [Fact]
        public void LoadValidTableTest()
        {
            var tableName = "table_test_1";
            var client = new AmazonDynamoDBClient();
            //Valid table - Non-composite Hash key of type String.
            var request = new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "primary_key",
                        KeyType = "HASH"
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "primary_key",
                        AttributeType = "S"
                    }
                },
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 1,
                    WriteCapacityUnits = 1
                }
            };
            //create the table here.
            CreateAndWaitUntilActive(client, request);
            var factory = GetFactory();
            //Use factory to load up cache.
            var cache = factory.Build(Options.Create(new DynamoDBDistributedCacheOptions
            {
                TableName = tableName,
                CreateTableIfNotExists = false
            }));
            //With lazy implementation, table creation is delayed until the client actually needs it.
            //resolving the table should pass. We then have a NotImplementedException until we implement CRUD operations.
            Assert.Throws<NotImplementedException>(() => cache.Get(""));
            CleanupTable(client, tableName);
        }

        /// <summary>
        /// Tests that our cache can reject a table if it is invalid. Invalid becuase Key is non-composite
        /// </summary>
        [Fact]
        public void LoadInvalidTable_TooManyKeysTest()
        {
            var tableName = "table_test_2";
            var client = new AmazonDynamoDBClient();
            var request = new CreateTableRequest
            //Invalid becuase key is non-composite
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "primary_key",
                        KeyType = "HASH"
                    },
                    new KeySchemaElement
                    {
                        AttributeName = "range_key2",
                        KeyType = "RANGE"
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "primary_key",
                        AttributeType = "S"
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "range_key2",
                        AttributeType = "N"
                    }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            };
            //Create table here
            CreateAndWaitUntilActive(client, request);
            DynamoDBDistributedCacheFactory factory = GetFactory();
            var cache = factory.Build(Options.Create<DynamoDBDistributedCacheOptions>(new DynamoDBDistributedCacheOptions
            {
                TableName = tableName,
                CreateTableIfNotExists = false

            }));
            //With lazy implementation, table creation is delayed until the client actually needs it.
            //resolving the table should not pass as the key is invalid.
            Assert.Throws<AmazonDynamoDBException>(() => cache.Get(""));
            CleanupTable(client, tableName);
        }

        /// <summary>
        /// Tests that our cache can reject a table if it is invalid. Invalid becuase Key is not a String
        /// </summary>
        [Fact]
        public void LoadInvalidTable_BadKeyTypeTest()
        {
            var tableName = "table_test_3";
            var client = new AmazonDynamoDBClient();
            var request = new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "primary_key",
                        KeyType = "HASH"
                    },
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "primary_key",
                        AttributeType = "N"
                    }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            };
            CreateAndWaitUntilActive(client, request);
            var factory = GetFactory();
            var cache = factory.Build(Options.Create<DynamoDBDistributedCacheOptions>(new DynamoDBDistributedCacheOptions
            {
                TableName = tableName,
                CreateTableIfNotExists = false
            }));
            //With lazy implementation, table creation is delayed until the client actually needs it.
            //resolving the table should not pass as the key is invalid.
            Assert.Throws<AmazonDynamoDBException>(() => cache.Get(""));
            CleanupTable(client, tableName);
        }

        private void CreateAndWaitUntilActive(AmazonDynamoDBClient client, CreateTableRequest request)
        {
            _ = client.CreateTableAsync(request).Result;
            WaitUntilActive(client, request.TableName);

        }

        private void WaitUntilActive(AmazonDynamoDBClient client, string tableName)
        {
            var isActive = false;
            while (!isActive)
            {
                var descRequest = new DescribeTableRequest
                {
                    TableName = tableName
                };
                var descResponse = client.DescribeTableAsync(descRequest).Result;
                var tableStatus = descResponse.Table.TableStatus;

                if (string.Equals(tableStatus, "Active", StringComparison.InvariantCultureIgnoreCase))
                    isActive = true;
            }
        }

        private void CleanupTable(AmazonDynamoDBClient client, string tableName)
        {
            WaitUntilActive(client, tableName);
            client.DeleteTableAsync(tableName).Wait();
            var exists = true;
            while (exists)
            {
                var task = client.ListTablesAsync();
                var resp = task.Result;
                if (!resp.TableNames.Contains(tableName))
                {
                    exists = false;
                }
            }
        }

        private DynamoDBDistributedCacheFactory GetFactory()
        {
            return new DynamoDBDistributedCacheFactory(new DynamoDBTableCreator(new ThreadSleeper()));
        }
    }
}
