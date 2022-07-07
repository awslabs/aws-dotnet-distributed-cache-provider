// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
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
                options.TableName = tablename;
            });
        }

        /// <summary>
        /// Injects <see cref="DynamoDBDistributedCache" /> as the implementation for <see cref="IDistributedCache"/>. Uses <see cref="DynamoDBDistributedCacheFactory"/> to create the cache.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="action"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static IServiceCollection AddAWSDynamoDBDistributedCache(this IServiceCollection services, Action<DynamoDBDistributedCacheOptions>? action = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddAWSService<IAmazonDynamoDB>();

            services.AddSingleton<IDynamoDBTableCreator, DynamoDBTableCreator>();
            //check if this is called twice there is not an issue
            services.AddOptions();
            if (action != null)
            {
                services.Configure(action);
            }
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, DynamoDBDistributedCache>((IServiceProvider p) => 
            {
                var client = p.GetRequiredService<IAmazonDynamoDB>();
                var options = p.GetRequiredService<IOptions<DynamoDBDistributedCacheOptions>>();
                var creator = p.GetRequiredService<IDynamoDBTableCreator>();
                return new DynamoDBDistributedCache(client, creator, options.Value);
            }));

            return services;
        }
    }
}
