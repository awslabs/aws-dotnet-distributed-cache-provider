// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
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
        public static IServiceCollection AddAWSDynamoDBDistributedCache(this IServiceCollection services, Action<DynamoDBDistributedCacheOptions> action)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            services.AddSingleton<IThreadSleeper, ThreadSleeper>();
            services.AddSingleton<IDynamoDBTableCreator, DynamoDBTableCreator>();
            services.AddSingleton<DynamoDBDistributedCacheFactory, DynamoDBDistributedCacheFactory>();

            services.AddOptions();
            services.Configure(action);
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, DynamoDBDistributedCache>((IServiceProvider p) => 
            {
                var options = p.GetRequiredService<IOptions<DynamoDBDistributedCacheOptions>>();
                return p.GetRequiredService<DynamoDBDistributedCacheFactory>().Build(options);
            }));

            return services;
        }
    }
}
