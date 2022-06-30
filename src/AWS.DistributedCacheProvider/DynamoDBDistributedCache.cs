// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Microsoft.Extensions.Caching.Distributed;
using Amazon.Runtime.Internal.Util;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AWS.DistributedCacheProvider
{
    public class DynamoDBDistributedCache : IDistributedCache
    {
        private readonly IAmazonDynamoDB _ddbClient;

        //configurable values
        private string _tableName { get; }
        private readonly bool _consistentReads;

        //Const values for columns
        public const string PRIMARY_KEY = "primary_key";//column that the key for the entry is stored
        public const string DEFAULT_TTL_ATTRIBUTE_NAME = "expdate";

        private readonly string TTL_ATTRIBUTE_NAME;

        private static readonly ILogger _logger = Logger.GetLogger(typeof(DynamoDBDistributedCache));

            public DynamoDBDistributedCache(AmazonDynamoDBClient client, string tableName, bool consistentReads)
        {
            if(client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if(tableName == null)
            {
                throw new ArgumentNullException(nameof(tableName));
            }
            _ddbClient = client;
            _tableName = tableName;
            _consistentReads = consistentReads;
            //Table should already exist when this class is instantiated. If it does not, a ResourceNotFoundException will be thrown here
            //Need to describe the table for validation and describe the table's TTL status
            var task_tableDesc = _ddbClient.DescribeTableAsync(_tableName);
            var task_ttlDesc = _ddbClient.DescribeTimeToLiveAsync(_tableName);
            //Table must be validated that it can serve as our cache
            ValidateTable(task_tableDesc.Result.Table);

            //Check if TTL is enabled on this table
            var ttlResp = task_ttlDesc.Result.TimeToLiveDescription;
            if(ttlResp.TimeToLiveStatus == TimeToLiveStatus.DISABLED || ttlResp.TimeToLiveStatus == TimeToLiveStatus.DISABLING)
            {
                //Log warning to user here that TTL for the table is currently off or being turned off
                //What is AWS convention for logging a warning?
            }
            TTL_ATTRIBUTE_NAME = ttlResp.AttributeName ?? DEFAULT_TTL_ATTRIBUTE_NAME;
        }

        private static void ValidateTable(TableDescription desc)
        {
            //This method was present in the old implementation, is it neccesary to have now also?
            var foundValidKey = false;
            foreach (var key in desc.KeySchema)
            {
                if (key.KeyType.Equals(KeyType.RANGE))
                {
                    throw new AmazonDynamoDBException(string.Format("Table {0} cannot be used as a cache because it contains a range key in its schema.", desc.TableName));
                }
                else //We know the key is of type Hash
                {
                    foreach (var attributeDef in desc.AttributeDefinitions)
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
                                    throw new AmazonDynamoDBException(string.Format("Table {0} cannot be used as a cache because it does not define a single hash key", desc.TableName));
                                }
                            }
                            else
                            {
                                throw new AmazonDynamoDBException(string.Format("Table {0} cannot be used as a cache because hash key is not a string.", desc.TableName));
                            }
                        }
                    }
                }
            }
        }
        
        public byte[] Get(string key)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public void Refresh(string key)
        {
            throw new NotImplementedException();
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public void Remove(string key)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            throw new NotImplementedException();
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
    }
}
