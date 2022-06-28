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
        public void CreateTableTest()
        {
            var cache = new DynamoDBDistributedCache();
            CleanupTable(cache.TableName());
        }

        [Fact]
        public void LoadValidTableTest()
        {
            var client = new AmazonDynamoDBClient();
            client.CreateTableAsync(new CreateTableRequest
            {
                TableName = ".NET_Cache",
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
            }).Wait();
            _ = new DynamoDBDistributedCache();
            CleanupTable(".NET_Cache");
        }

        [Fact]
        public void LoadInvalidTable_TooManyKeysTest()
        {
            var client = new AmazonDynamoDBClient();
            client.CreateTableAsync(new CreateTableRequest
            {
                TableName = ".NET_Cache",
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
            }).Wait();
            Assert.Throws<AmazonDynamoDBException>(() => new DynamoDBDistributedCache());
            CleanupTable(".NET_Cache");
        }

        [Fact]
        public void LoadInvalidTable_BadKeyTypeTest()
        {
            var client = new AmazonDynamoDBClient();
            client.CreateTableAsync(new CreateTableRequest
            {
                TableName = ".NET_Cache",
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
            }).Wait();
            Assert.Throws<AmazonDynamoDBException>(() => new DynamoDBDistributedCache());
            CleanupTable(".NET_Cache");
        }

        private void CleanupTable(string tableName)
        {
            var client = new AmazonDynamoDBClient();
            // Wait till table is active
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
