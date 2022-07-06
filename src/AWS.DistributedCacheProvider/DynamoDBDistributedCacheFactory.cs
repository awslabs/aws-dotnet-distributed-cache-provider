// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Options;

namespace AWS.DistributedCacheProvider
{
    public  class DynamoDBDistributedCacheFactory
    {
        private readonly IDynamoDBTableCreator _creator;
        public DynamoDBDistributedCacheFactory(IDynamoDBTableCreator creator)
        {
            _creator = creator;
        }

        public DynamoDBDistributedCache Build(IOptions<DynamoDBDistributedCacheOptions> opts)
        {
            DynamoDBDistributedCacheOptions options = opts.Value;
            AmazonDynamoDBClient client;
            if (options.Credentials != null && options.DynamoConfig != null)
            {
                client = new AmazonDynamoDBClient(options.Credentials, options.DynamoConfig);
            }
            else if (options.Credentials != null)
            {
                client = new AmazonDynamoDBClient(options.Credentials);
            }
            else if (options.DynamoConfig != null)
            {
                client = new AmazonDynamoDBClient(options.DynamoConfig);
            }
            else
            {
                client = new AmazonDynamoDBClient();
            }
            var name = options.TableName;
            var ttl = options.EnableTtl;
            var ttlAttribute = options.TTLAttributeName;
            var create = options.CreateTableIfNotExists;
            var consistentReads = options.ConsistentReads;
            //Using Lazy implementation to avoid Async calls done during DI.
            Lazy<Task<IAmazonDynamoDB>> lazyClient = new Lazy<Task<IAmazonDynamoDB>>( async () =>
            {
                await _creator.CreateIfNotExistsAsync(client, name, create, ttl, ttlAttribute);
                return client;
            });
            Lazy<Task<string>> lazyTTLAttribute = new Lazy<Task<string>>(async () =>
            {
                return await _creator.GetTTLColumn(client, name);
            });
            return new DynamoDBDistributedCache(lazyClient, options, lazyTTLAttribute);
        }
    }
}
