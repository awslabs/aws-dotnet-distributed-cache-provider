// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Caching.Distributed;

namespace AWS.DistributedCacheProvider.Internal
{
    /// <summary>
    /// A helper class that calculates TTL related information for <see cref="DynamoDBDistributedCache"/>
    /// This class is not meant to be called directly by a client, it is only kept public for testing purposes.
    /// If you need to rely on this class, consider opening a
    /// <see href="https://github.com/aws/aws-dotnet-distributed-cache-provider/issues/new/choose">feature request</see>
    /// </summary>
    public class DynamoDBCacheProviderHelper
    {
        /// <summary>
        /// Calculates the absolute Time To Live (TTL) given the <paramref name="options"/>
        /// </summary>
        /// <param name="options">A <see cref="DistributedCacheEntryOptions"/> which is used to calculate the TTL deadline of an Item</param>
        /// <returns>An <see cref="AttributeValue"/> which contains either the absolute deadline TTL or nothing.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When the calculated absolute deadline is in the past.</exception>
        public static AttributeValue CalculateTTLDeadline(DistributedCacheEntryOptions options)
        {
            if (options.AbsoluteExpiration == null && options.AbsoluteExpirationRelativeToNow == null)
            {
                return new AttributeValue { NULL = true };
            }
            else if (options.AbsoluteExpiration != null && options.AbsoluteExpirationRelativeToNow == null)
            {
                var ttl = (DateTimeOffset)options.AbsoluteExpiration;
                var now = DateTimeOffset.UtcNow;
                if (now.CompareTo(ttl) > 0)//if ttl is before current time
                {
                    throw new ArgumentOutOfRangeException("AbsoluteExpiration must be in the future.");
                }
                else
                {
                    return new AttributeValue { N = ttl.ToUnixTimeSeconds().ToString() };
                }
            }//AbsoluteExpirationRelativeToNow is not null, regardless of what AbsoluteExpiration is set to, we prefer AbsoluteExpirationRelativeToNow
            else
            {
                var ttl = DateTimeOffset.UtcNow.Add((TimeSpan)options.AbsoluteExpirationRelativeToNow!).ToUnixTimeSeconds();
                return new AttributeValue { N = ttl.ToString() };
            }
        }

        /// <summary>
        /// Calculates the TTL.
        /// </summary>
        /// <param name="options">A <see cref="DistributedCacheEntryOptions"/> which is used to calculate the TTL of an Item</param>
        /// <returns>An <see cref="AttributeValue"/> containting the TTL</returns>
        public static AttributeValue CalculateTTL(DistributedCacheEntryOptions options)
        {
            //if the sliding window is present, then now + window
            if (options.SlidingExpiration != null)
            {
                var ttl = DateTimeOffset.UtcNow.Add(((TimeSpan)options.SlidingExpiration));
                //Cannot be later than the deadline
                var absoluteTTL = CalculateTTLDeadline(options);
                if (absoluteTTL.NULL)
                {
                    return new AttributeValue { N = ttl.ToUnixTimeSeconds().ToString() };
                }
                else //return smaller of the two. Either the TTL based on the sliding window or the deadline
                {
                    if (long.Parse(absoluteTTL.N) < ttl.ToUnixTimeSeconds())
                    {
                        return absoluteTTL;
                    }
                    else
                    {
                        return new AttributeValue { N = ttl.ToUnixTimeSeconds().ToString() };
                    }
                }
            }
            else //just return the absolute TTL
            {
                return CalculateTTLDeadline(options);
            }
        }

        /// <summary>
        /// Returns the sliding window of the TTL
        /// </summary>
        /// <param name="options"></param>
        /// <returns>An <see cref="AttributeValue"/> which either contains a string version of the sliding window <see cref="TimeSpan"/>
        ///  or nothing</returns>
        public static AttributeValue CalculateSlidingWindow(DistributedCacheEntryOptions options)
        {
            if (options.SlidingExpiration != null)
            {
                return new AttributeValue { S = options.SlidingExpiration.ToString() };
            }
            else
            {
                return new AttributeValue { NULL = true };
            }
        }

    }
}
