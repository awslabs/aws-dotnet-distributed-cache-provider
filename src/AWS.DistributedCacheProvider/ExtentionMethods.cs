// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.DistributedCacheProvider;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ExtentionMethods
    {

        public static IServiceCollection AddAWSDynamoDBDistributedCache(this IServiceCollection services, string tablename)
        {
            return AddAWSDynamoDBDistributedCache(services, options =>
            {
                options.tablename = tablename;
            });
        }
        public static IServiceCollection AddAWSDynamoDBDistributedCache(this IServiceCollection services, Action<DynamoDBDistributedCacheOptions> action)
        {
            if(services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if(action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            services.AddOptions();
            services.Configure(action);
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, DynamoDBDistributedCache>((IServiceProvider p) =>
            {
                AmazonDynamoDBClient client;
                IOptions<DynamoDBDistributedCacheOptions> options = p.GetRequiredService<IOptions<DynamoDBDistributedCacheOptions>>();
                if (options.Value.credentials != null && options.Value.dynamoConfig != null)
                {
                    client = new AmazonDynamoDBClient(options.Value.credentials, options.Value.dynamoConfig);
                } else if (options.Value.credentials != null)
                {
                    client = new AmazonDynamoDBClient(options.Value.credentials);
                } else if (options.Value.dynamoConfig != null)
                {
                    client = new AmazonDynamoDBClient(options.Value.dynamoConfig);
                } else
                {
                    client = new AmazonDynamoDBClient();
                }
                var name = options.Value.tablename;
                var ttl = options.Value.enableTtl;
                var ttlAttribute = options.Value.ttlAttributeName;
                var create = options.Value.createTableIfNotExists;
                var consistentReads = options.Value.consistentReads;

                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException("The table name cannot be null or empty!");
                }

                try
                {
                    //test if table already exists
                    DescribeTableResponse? resp = client.DescribeTableAsync(new DescribeTableRequest
                    {
                        TableName = name,

                    }).Result;
                    //leave table validation to the actual cache class. No need to check twice.
                }
                catch (ResourceNotFoundException)
                {//thrown when table does not already exist
                    if (create)
                        CreateTable(client, name, ttl, ttlAttribute);
                    else
                        throw new AmazonDynamoDBException(string.Format("Table {0} was not found to be used as cache and autocreate is turned off.", name));
                }

                return new DynamoDBDistributedCache(client, name, consistentReads);
            }));

            return services;
        }

        private static void CreateTable(AmazonDynamoDBClient client, string tablename, bool enableTTL, string ttlAttribute)
        {
            var createRequest = new CreateTableRequest
            {
                TableName = tablename,
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

            var descRequest = new DescribeTableRequest
            {
                TableName = tablename
            };

            // Wait till table is active
            var isActive = false;
            while (!isActive)
            {
                Thread.Sleep(5000);
                var descResponse = client.DescribeTableAsync(descRequest).Result;
                var tableStatus = descResponse.Table.TableStatus;

                if (string.Equals(tableStatus, "Active", StringComparison.InvariantCultureIgnoreCase))
                    isActive = true;
            }
            if (enableTTL)
            {
                client.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
                {
                    TableName = tablename,
                    TimeToLiveSpecification = new TimeToLiveSpecification
                    {
                        AttributeName = ttlAttribute ?? DynamoDBDistributedCache.DEFAULT_TTL_ATTRIBUTE_NAME,
                        Enabled = true
                    }
                });
            }
        }
    }
}
