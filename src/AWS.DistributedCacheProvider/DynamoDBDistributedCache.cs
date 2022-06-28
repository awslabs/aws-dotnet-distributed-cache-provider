// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Microsoft.Extensions.Caching.Distributed;
using Amazon.Runtime.Internal.Util;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

namespace AWS.DistributedCacheProvider
{
    public class DynamoDBDistributedCache : IDistributedCache
    {
        const string DEFAULT_TABLENAME = ".NET_Cache";
        IAmazonDynamoDB _ddbClient;
        Table _table;

        //configurable values
        private string _tableName { get; } = DEFAULT_TABLENAME;
        private bool _createIfNotExist { get; } = true;
        private int _initialReadUnits { get; } = 10;
        private int _initialWriteUnits { get; } = 5;
        private const int DESCRIBE_INTERVAL = 5000;
        private const string ACTIVE_STATUS = "Active";

        //Const values for columns
        private const string PRIMARY_KEY = "primary_key";//column that the key for the entry is stored

        private static readonly ILogger _logger = Logger.GetLogger(typeof(DynamoDBDistributedCache));

        public DynamoDBDistributedCache()
        {
            _ddbClient = new AmazonDynamoDBClient();
            try
            {
                var tabbleConfig = new TableConfig(DEFAULT_TABLENAME)
                {
                    Conversion = DynamoDBEntryConversion.V2
                };
                _table = Table.LoadTable(_ddbClient, tabbleConfig);
            }
            catch (ResourceNotFoundException) { }

            if (_table == null)
            {
                if (_createIfNotExist)
                    _table = CreateTable();
                else
                    throw new AmazonDynamoDBException(string.Format("Table {0} was not found to be used to store session state and autocreate is turned off.", _tableName));
            }
            else
            {
                ValidateTable();
            }
        }

        public string TableName()
        {
            return _tableName;
        }

        private void ValidateTable()
        {
            //This method was present in the old implementation, is it neccesary to have now also?
            if (_table.HashKeys.Count != 1)
                throw new AmazonDynamoDBException(string.Format("Table {0} cannot be used as a cache because it does not define a single hash key", _tableName));
            var hashKey = _table.HashKeys[0];
            KeyDescription hashKeyDescription = _table.Keys[hashKey];
            if (hashKeyDescription.Type != DynamoDBEntryType.String)
                throw new AmazonDynamoDBException(string.Format("Table {0} cannot be used as a cache because hash key is not a string.", _tableName));

            if (_table.RangeKeys.Count > 0)
                throw new AmazonDynamoDBException(string.Format("Table {0} cannot be used as a cache because it contains a range key in its schema.", _tableName));
        }

        private Table CreateTable()
        {
            CreateTableRequest createRequest = new CreateTableRequest
            {
                TableName = _tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = PRIMARY_KEY,
                        KeyType = "HASH"
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = PRIMARY_KEY,
                        AttributeType = "S"
                    }
                },
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = _initialReadUnits,
                    WriteCapacityUnits = _initialWriteUnits
                }
            };

            _ddbClient.CreateTableAsync(createRequest);

            DescribeTableRequest descRequest = new DescribeTableRequest
            {
                TableName = _tableName
            };

            // Wait till table is active
            bool isActive = false;
            while (!isActive)
            {
                Thread.Sleep(DESCRIBE_INTERVAL);
                DescribeTableResponse descResponse = _ddbClient.DescribeTableAsync(descRequest).Result;
                string tableStatus = descResponse.Table.TableStatus;

                if (string.Equals(tableStatus, ACTIVE_STATUS, StringComparison.InvariantCultureIgnoreCase))
                    isActive = true;
            }

            var tableConfig = new TableConfig(_tableName)
            {
                Conversion = DynamoDBEntryConversion.V1
            };
            Table table = Table.LoadTable(_ddbClient, tableConfig);
            return table;
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
