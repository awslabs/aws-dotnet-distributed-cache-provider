// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Microsoft.Extensions.Caching.Distributed;
using Amazon.Runtime.Internal.Util;
using Amazon.DynamoDBv2;

namespace AWS.DistributedCacheProvider
{
    public class DynamoDBDistributedCache : IDistributedCache
    {
        private IAmazonDynamoDB? _ddbClient;
        private IDynamoDBTableCreator _dynamodbTableCreator;
        private bool _started;

        //configurable values
        private string _tableName { get; }
        private readonly bool _consistentReads;
        private string? _ttl_attribute_name;
        private readonly bool _createTableifNotExists;

        //Const values for columns
        public const string PRIMARY_KEY = "primary_key";//column that the key for the entry is stored
        public const string DEFAULT_TTL_ATTRIBUTE_NAME = "expdate";


        private static readonly ILogger _logger = Logger.GetLogger(typeof(DynamoDBDistributedCache));

        /// <summary>
        /// Constructor for DynamoDBDistributedCache.
        /// </summary>
        /// <param name="client">DynamoDB Client</param>
        /// <param name="creator">Creator class that is responsible for creating or validating the DynamoDB Table</param>
        /// <param name="options">Configurable options for the cache</param>
        /// <exception cref="ArgumentNullException"></exception>
        public DynamoDBDistributedCache(IAmazonDynamoDB client, IDynamoDBTableCreator creator, DynamoDBDistributedCacheOptions options)
        {
            if(client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }
            _ddbClient = client;
            _dynamodbTableCreator = creator;
            _consistentReads = options.ConsistentReads;
            _tableName = options.TableName;
            _ttl_attribute_name = options.TTLAttributeName;
            _createTableifNotExists = options.CreateTableIfNotExists;
        }

        /// <summary>
        /// Make sure the backing datastore is up and running before accepting client requests
        /// </summary>
        private async Task StartupAsync()
        {
            //future PR. Make this Thread Safe
            if(!_started)
            {
                //future PR. This should reduced to a single method call. If table is being created, no need for TTL describe...
                await _dynamodbTableCreator.CreateTableIfNotExistsAsync(_ddbClient, _tableName, _createTableifNotExists, _ttl_attribute_name);
                _ttl_attribute_name = await _dynamodbTableCreator.GetTTLColumn(_ddbClient, _tableName);
                _started = true;
            }
        }
        
        public byte[] Get(string key)
        {
            StartupAsync().GetAwaiter().GetResult();
            throw new NotImplementedException();
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            await StartupAsync();
            throw new NotImplementedException();
        }

        public void Refresh(string key)
        {
            StartupAsync().GetAwaiter().GetResult();
            throw new NotImplementedException();
        }

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            await StartupAsync();
            throw new NotImplementedException();
        }

        public void Remove(string key)
        {
            StartupAsync().GetAwaiter().GetResult();
            throw new NotImplementedException();
        }

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            await StartupAsync();
            throw new NotImplementedException();
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            StartupAsync().GetAwaiter().GetResult();
            throw new NotImplementedException();
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            await StartupAsync();
            throw new NotImplementedException();
        }
    }
}
