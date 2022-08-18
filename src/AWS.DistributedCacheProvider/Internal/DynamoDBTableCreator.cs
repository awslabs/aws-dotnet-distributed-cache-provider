// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AWS.DistributedCacheProvider.Internal
{
    /// <summary>
    /// A helper class that manages DynamoDB interactions related to Table creation, loading, and validation.
    /// This class is not meant to be called directly by a client, it is only kept public for testing purposes
    /// If you need to rely on this class, consider opening a
    /// <see href="https://github.com/aws/aws-dotnet-distributed-cache-provider/issues/new/choose">feature request</see>
    /// </summary>
    public class DynamoDBTableCreator : IDynamoDBTableCreator
    {
        private readonly ILogger<DynamoDBTableCreator> _logger;

        public DynamoDBTableCreator(ILoggerFactory? loggerFactory = null)
        {
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<DynamoDBTableCreator>();
            }
            else
            {
                _logger = NullLoggerFactory.Instance.CreateLogger<DynamoDBTableCreator>();
            }
        }

        /// <inheritdoc/>
        public async Task<string> CreateTableIfNotExistsAsync(IAmazonDynamoDB client, string tableName, bool create, string? ttlAttribute)
        {
            _logger.LogDebug($"Create If Not Exists called. Table name: {tableName}, Create If Not Exists: {create}.");
            try
            {
                //test if table already exists
                var resp = await client.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                });
                _logger.LogDebug("Table does exist. Validating");
                var primary_key = ValidateTable(resp.Table);
                _logger.LogInformation($"DynamoDB distributed cache provider configured to use table {tableName}. Primary key is {primary_key}");
                return primary_key;
            }
            catch (ResourceNotFoundException) //thrown when table does not already exist
            {
                _logger.LogDebug("Table does not exist");
                if (create)
                {
                    var primary_key = await CreateTableAsync(client, tableName, ttlAttribute);
                    _logger.LogInformation($"DynamoDB distributed cache provider created table {tableName}. Primary key is {primary_key}");
                    return primary_key;
                }
                else
                {
                    throw new InvalidTableException($"Table {tableName} was not found to be used as cache and autocreate is turned off.");
                }
            }
        }

        /// <summary>
        /// Verifies that the key schema for this table is a non-composite, Hash key of type String.
        /// </summary>
        /// <param name="description">A table description <see cref="TableDescription"/></param>
        /// <exception cref="InvalidTableException">Thrown when key Schema is invalid</exception>
        private string ValidateTable(TableDescription description)
        {
            var primaryKeyName = "";
            foreach (var key in description.KeySchema)
            {
                if (key.KeyType.Equals(KeyType.RANGE))
                {
                    throw new InvalidTableException($"Table {description.TableName} cannot be used as a cache because it contains" +
                        $" a range key in its schema. Cache requires a non-composite Hash key of type String.");
                }
                else //We know the key is of type Hash
                {
                    foreach (var attributeDef in description.AttributeDefinitions)
                    {
                        if (attributeDef.AttributeName.Equals(key.AttributeName) && attributeDef.AttributeType != ScalarAttributeType.S)
                        {
                            throw new InvalidTableException($"Table {description.TableName} cannot be used as a cache because hash key " +
                                 $"is not a string. Cache requires a non-composite Hash key of type String.");
                        }
                    }
                }
                //If there is an element in the key schema that is of type Hash and is a string, it must be the primary key
                primaryKeyName = key.AttributeName;
            }
            return primaryKeyName;
        }

        /// <summary>
        /// Creates a table that is usable as a cache.
        /// </summary>
        /// <param name="client">DynamoDB client</param>
        /// <param name="tableName">Table name</param>
        /// <param name="ttlAttribute">TTL attribute name</param>
        private async Task<string> CreateTableAsync(IAmazonDynamoDB client, string tableName, string? ttlAttribute)
        {
            var primary_key_name = DynamoDBDistributedCache.DEFAULT_PRIMARY_KEY;
            var createRequest = new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = primary_key_name,
                        KeyType = KeyType.HASH
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = primary_key_name,
                        AttributeType = ScalarAttributeType.S
                    }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            };

            await client.CreateTableAsync(createRequest);

            // Wait untill table is active
            var isActive = false;
            while (!isActive)
            {
                var tableStatus = (await (client.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                }))).Table.TableStatus;
                if (tableStatus == TableStatus.ACTIVE)
                {
                    isActive = true;
                }
                else
                {
                    await Task.Delay(5000);
                }
            }
            await client.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
            {
                TableName = tableName,
                TimeToLiveSpecification = new TimeToLiveSpecification
                {
                    AttributeName = ttlAttribute ?? DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME,
                    Enabled = true
                }
            });
            return primary_key_name;
        }

        /// <inheritdoc/>
        public async Task<string> GetTTLColumnAsync(IAmazonDynamoDB client, string tableName)
        {
            var ttlDesc = (await client.DescribeTimeToLiveAsync(tableName)).TimeToLiveDescription;
            if (ttlDesc.TimeToLiveStatus == TimeToLiveStatus.DISABLED || ttlDesc.TimeToLiveStatus == TimeToLiveStatus.DISABLING)
            {
                _logger.LogWarning($"Distributed cache table {tableName} has Time to Live (TTL) disabled. Items will never be deleted " +
                    $"automatically. It is recommended to enable TTL for the table to remove stale cached data.");
            }
            return ttlDesc.AttributeName ?? DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME;
        }
    }
}
