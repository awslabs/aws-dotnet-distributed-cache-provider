// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.DistributedCacheProvider;
using Xunit;

namespace AWS.DistributedCacheProviderTests
{
    public class DynamoDBDistributedCacheTableTests
    {
        [Fact]
        public void LoadValidTableTest()
        {
            var tableName = "table_test_1";
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
            CreateAndWaitUntilActive(client, request);
            _ = new DynamoDBDistributedCache(client, tableName, false);
            CleanupTable(client, tableName);
        }

        [Fact]
        public void LoadInvalidTable_TooManyKeysTest()
        {
            var tableName = "table_test_2";
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
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 1,
                    WriteCapacityUnits = 1
                }
            };
            CreateAndWaitUntilActive(client, request);
            Assert.Throws<AmazonDynamoDBException>(() => new DynamoDBDistributedCache(client, tableName, false));
            CleanupTable(client, tableName);
        }

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
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 1,
                    WriteCapacityUnits = 1
                }
            };
            CreateAndWaitUntilActive(client, request);
            Assert.Throws<AmazonDynamoDBException>(() => new DynamoDBDistributedCache(client, tableName, false));
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
                DescribeTableRequest descRequest = new DescribeTableRequest
                {
                    TableName = tableName
                };
                DescribeTableResponse descResponse = client.DescribeTableAsync(descRequest).Result;
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
                Task<ListTablesResponse> task = client.ListTablesAsync();
                ListTablesResponse resp = task.Result;
                if (!resp.TableNames.Contains(tableName))
                {
                    exists = false;
                }
            }
        }
    }
}
