// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime.Internal.Util;

namespace AWS.DistributedCacheProvider
{
    public class DynamoDBTableCreator : IDynamoDBTableCreator
    {
        private static readonly ILogger s_logger = Logger.GetLogger(typeof(DynamoDBTableCreator));
        private readonly IThreadSleeper _sleeper;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sleeper">Sleeper object that we call to abstract out Thread.Sleep(). We abstract the method call to make testing easier.</param>
        public DynamoDBTableCreator(IThreadSleeper sleeper)
        {
            _sleeper = sleeper;
        }

        /// <inheritdoc/>
        public async Task CreateIfNotExistsAsync(IAmazonDynamoDB client, string tableName, bool create, bool enableTTL, string ttlAttribute)
        {
            s_logger.InfoFormat($"Create If Not Exists called. Table name: {tableName}, Create If Not Exists: {create}");
            try
            {
                //test if table already exists
                var resp = await client.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                });
                s_logger.InfoFormat("Table does exist. Validating");
                ValidateTable(resp.Table);
            }
            catch (ResourceNotFoundException) //thrown when table does not already exist
            {
                s_logger.InfoFormat("Table does not exist");
                if (create)
                {
                    CreateTable(client, tableName, enableTTL, ttlAttribute);
                }
                else
                {
                    throw new AmazonDynamoDBException($"Table {tableName} was not found to be used as cache and autocreate is turned off.");
                }
            }
        }

        /// <summary>
        /// Verifies that the key schema for this table is a non-composite, Hash key of type String.
        /// </summary>
        /// <param name="description">A table description <see cref="TableDescription"/></param>
        /// <exception cref="AmazonDynamoDBException">Thrown when key Schema is invalid</exception>
        private void ValidateTable(TableDescription description)
        {
            var foundValidKey = false;
            foreach (var key in description.KeySchema)
            {
                if (key.KeyType.Equals(KeyType.RANGE))
                {
                    throw new AmazonDynamoDBException($"Table {description.TableName} cannot be used as a cache because it contains a range key in its schema.");
                }
                else //We know the key is of type Hash
                {
                    foreach (var attributeDef in description.AttributeDefinitions)
                    {
                        if (attributeDef.AttributeName.Equals(key.AttributeName))
                        {
                            if (attributeDef.AttributeType.Equals(new ScalarAttributeType("S")))
                            {
                                if (!foundValidKey)
                                {
                                    foundValidKey = true;
                                }
                                else
                                {
                                    throw new AmazonDynamoDBException($"Table {description.TableName} cannot be used as a cache because it does not define a single hash key");
                                }
                            }
                            else
                            {
                                throw new AmazonDynamoDBException($"Table {description.TableName} cannot be used as a cache because hash key is not a string.");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a table that is usable for the client
        /// </summary>
        /// <param name="client">DynamoDB client</param>
        /// <param name="tableName">Table name</param>
        /// <param name="enableTTL">Enable TTL on the table</param>
        /// <param name="ttlAttribute">TTL attribute name</param>
        private void CreateTable(IAmazonDynamoDB client, string tableName, bool enableTTL, string ttlAttribute)
        {
            var createRequest = new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = DynamoDBDistributedCache.PRIMARY_KEY,
                        KeyType = "HASH"
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = DynamoDBDistributedCache.PRIMARY_KEY,
                        AttributeType = "S"
                    }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            };

            client.CreateTableAsync(createRequest);

            // Wait untill table is active
            var isActive = false;
            while (!isActive)
            {
                _sleeper.Sleep(5000);
                var tableStatus = client.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                }).Result.Table.TableStatus;

                if (string.Equals(tableStatus, "Active", StringComparison.InvariantCultureIgnoreCase))
                    isActive = true;
            }
            if (enableTTL)
            {
                client.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = tableName,
                    TimeToLiveSpecification = new TimeToLiveSpecification
                    {
                        AttributeName = ttlAttribute ?? DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME,
                        Enabled = true
                    }
                });
            }
            else
            {
                s_logger.InfoFormat($"Creating table {tableName}, however TTL has not been enabled. Items will never be deleted automatically");
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetTTLColumn(IAmazonDynamoDB client, string tableName)
        {
            var ttlDesc = (await client.DescribeTimeToLiveAsync(tableName)).TimeToLiveDescription;
            if (ttlDesc.TimeToLiveStatus == TimeToLiveStatus.DISABLED || ttlDesc.TimeToLiveStatus == TimeToLiveStatus.DISABLING)
            {
                s_logger.InfoFormat($"Loading table {tableName} and current TTL status is {ttlDesc.TimeToLiveStatus}. Items will never be deleted automatically");
            }
            return ttlDesc.AttributeName ?? DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME;
        }
    }
}
