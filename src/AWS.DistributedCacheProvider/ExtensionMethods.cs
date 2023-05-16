// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
using AWS.DistributedCacheProvider;
using AWS.DistributedCacheProvider.Internal;
using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Injects <see cref="DynamoDBDistributedCache" /> as the implementation for <see cref="IDistributedCache"/>.
        /// </summary>
        /// <param name="services">The current ServiceCollection</param>
        /// <param name="action">An Action to configure the parameters of <see cref="DynamoDBDistributedCacheOptions"/> for the cache</param>
        /// <exception cref="ArgumentNullException">Thrown when one of the required parameters is null</exception>
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

            // Ensure there is an DynamoDB client added, though using Try in case the user added their own
            services.TryAddAWSService<IAmazonDynamoDB>();

            // The TableCreator is an internal dependency
            services.AddSingleton<IDynamoDBTableCreator, DynamoDBTableCreator>();

            // Configure the Action the user provided
            services.AddOptions();
            services.Configure(action);

            // Now that the three required parameters are added, add the cache implementation
            services.AddSingleton<IDistributedCache, DynamoDBDistributedCache>();

            return services;
        }
    }
}
