// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime.Internal.Util;

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
        private static readonly ILogger _logger = Logger.GetLogger(typeof(DynamoDBTableCreator));

        /// <inheritdoc/>
        public async Task CreateTableIfNotExistsAsync(IAmazonDynamoDB client, string tableName, bool create, string? ttlAttribute)
        {
            _logger.InfoFormat($"Create If Not Exists called. Table name: {tableName}, Create If Not Exists: {create}");
            try
            {
                //test if table already exists
                var resp = await client.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                });
                _logger.InfoFormat("Table does exist. Validating");
                ValidateTable(resp.Table);
            }
            catch (ResourceNotFoundException) //thrown when table does not already exist
            {
                _logger.InfoFormat("Table does not exist");
                if (create)
                {
                    await CreateTableAsync(client, tableName, ttlAttribute);
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
        private void ValidateTable(TableDescription description)
        {
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
            }
        }

        /// <summary>
        /// Creates a table that is usable as a cache.
        /// </summary>
        /// <param name="client">DynamoDB client</param>
        /// <param name="tableName">Table name</param>
        /// <param name="ttlAttribute">TTL attribute name</param>
        private async Task CreateTableAsync(IAmazonDynamoDB client, string tableName, string? ttlAttribute)
        {
            var createRequest = new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = DynamoDBDistributedCache.PRIMARY_KEY,
                        KeyType = KeyType.HASH
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = DynamoDBDistributedCache.PRIMARY_KEY,
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
        }

        /// <inheritdoc/>
        public async Task<string> GetTTLColumnAsync(IAmazonDynamoDB client, string tableName)
        {
            var ttlDesc = (await client.DescribeTimeToLiveAsync(tableName)).TimeToLiveDescription;
            if (ttlDesc.TimeToLiveStatus == TimeToLiveStatus.DISABLED || ttlDesc.TimeToLiveStatus == TimeToLiveStatus.DISABLING)
            {
                _logger.InfoFormat($"Loading table {tableName} and current TTL status is {ttlDesc.TimeToLiveStatus}. Items will never be deleted automatically");
            }
            return ttlDesc.AttributeName ?? DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME;
        }
    }
}
