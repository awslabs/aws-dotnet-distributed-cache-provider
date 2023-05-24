// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace AWS.DistributedCacheProvider.Internal
{
    public static class Utilities
    {
        /// <summary>
        /// Format the partition key value applying user specified key prefix. The prefix "dc:" is always
        /// added to namespace the cache items in the table allowing the table to be used in a single table
        /// pattern by default for most use cases.
        /// </summary>
        /// <param name="partitionKeyValue"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatPartitionKey(string partitionKeyValue, string? partitionKeyPrefix)
        {
            if (string.IsNullOrEmpty(partitionKeyPrefix))
            {
                return $"dc:{partitionKeyValue}";
            }

            return $"{partitionKeyPrefix}:dc:{partitionKeyValue}";
        }
    }
}
