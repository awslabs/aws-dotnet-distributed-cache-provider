// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Microsoft.Extensions.Caching.Distributed;
using Amazon.Runtime.Internal.Util;
using Amazon.DynamoDBv2;

namespace AWS.DistributedCacheProvider
{
    public class DynamoDBDistributedCache : IDistributedCache
    {
        private readonly Lazy<Task<IAmazonDynamoDB>> _lazyClient;
        private IAmazonDynamoDB? _ddbClient;
        private readonly Lazy<Task<string>> _lazyTTLAttribute;

        //configurable values
        private string _tableName { get; }
        private readonly bool _consistentReads;
        private string? _ttl_attribute_name;

        //Const values for columns
        public const string PRIMARY_KEY = "primary_key";//column that the key for the entry is stored
        public const string DEFAULT_TTL_ATTRIBUTE_NAME = "expdate";


        private static readonly ILogger _logger = Logger.GetLogger(typeof(DynamoDBDistributedCache));

        /// <summary>
        /// Constructor for DynamoDBDistributedCache. Do not use directly, use <see cref="DynamoDBDistributedCacheFactory"/> instead.
        /// Data that required Internet IO is done with Lazy objects. This allows DI to be completed without depending on Async method calls
        /// </summary>
        /// <param name="lazyClient">Lazy lambda that either verifies the table or creates it if specified before returning an IAmazonDynamoDB</param>
        /// <param name="options">Options that include configuartion data for the cache</param>
        /// <param name="lazyTTLAttribute">Lazy Lambda that determines the TTL status of the table and what column to use for TTL</param>
        /// <exception cref="ArgumentNullException"></exception>
        public DynamoDBDistributedCache(Lazy<Task<IAmazonDynamoDB>> lazyClient, DynamoDBDistributedCacheOptions options, Lazy<Task<string>> lazyTTLAttribute)
        {
            if(lazyClient == null)
            {
                throw new ArgumentNullException(nameof(lazyClient));
            }
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            _lazyClient = lazyClient;
            _tableName = options.TableName;
            _consistentReads = options.ConsistentReads;
            _lazyTTLAttribute = lazyTTLAttribute;
        }

        /// <summary>
        /// Resolves Lazy lambdas passed in via constructor
        /// </summary>
        private void ResolveLazy()
        {
            try
            {
                if (_ddbClient == null)
                {
                    _logger.InfoFormat("Resolving DynamoDBClient lazy object");
                    _ddbClient = _lazyClient.Value.Result;
                }
                if (_ttl_attribute_name == null)
                {
                    _logger.InfoFormat("Resolving TTL attribute lazy object");
                    _ttl_attribute_name = _lazyTTLAttribute.Value.Result ?? DEFAULT_TTL_ATTRIBUTE_NAME;
                }
            }
            catch (AggregateException e)
            {
                throw e.InnerException;
            }
        }
        
        public byte[] Get(string key)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }

        public void Refresh(string key)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }

        public void Remove(string key)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            ResolveLazy();
            throw new NotImplementedException();
        }
    }
}
