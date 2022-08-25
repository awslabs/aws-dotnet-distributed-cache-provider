// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace AWS.DistributedCacheProvider
{
    /// <summary>
    /// Configurable parameters for DynamoDBDistributedCache
    /// </summary>
    public class DynamoDBDistributedCacheOptions : IOptions<DynamoDBDistributedCacheOptions>
    {
        /// <summary>
        /// Required parameter. The name of the backing DynamoDB Table. Cannot be Null or Empty
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Optional parameter. If the table tablename does not exist, create the table.  Default is false
        /// </summary>
        public bool CreateTableIfNotExists { get; set; }

        /// <summary>
        /// Optional parameter. Must reads from the underlying Table be consistent  Default is true.
        /// Having consistent reads means that any read will be from the latest data in the DynamoDB cluster.
        /// However, it does come at a performance hit. Changing this to false means that inconsistent reads can occur.
        /// </summary>
        public bool ConsistentReads { get; set; } = true;

        /// <summary>
        /// Optional parameter. Name of the TTL column when Table is created here.
        /// </summary>
        public string? TTLAttributeName { get; set; }

        /// <summary>
        /// Optional parameter. Mane of the Primary Key attribute when Table is created here.
        /// </summary>
        public string? PrimaryKeyName { get; set; }

        public DynamoDBDistributedCacheOptions Value
        {
            get { return this; }
        }
    }
}
