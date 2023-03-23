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
        /// Required parameter. The name of the DynamoDB Table to store cached data.
        /// </summary>
        public string? TableName { get; set; }

        /// <summary>
        /// If set to true during startup the library will check if the specified table exists. If the
        /// table does not exist a new table will be created using on demand provision throughput.
        /// This will require extra permissions to call DescribeTable, CreateTable and UpdateTimeToLive
        /// service operations.
        /// It is recommended to only set to true in development environments. Production environments
        /// should create the table before deployment and set this property to false. This will 
        /// allow production deployments to have less permissions and have faster startup.
        /// <para>
        /// Default is false
        /// </para>
        /// </summary>
        public bool CreateTableIfNotExists { get; set; }

        /// <summary>
        /// Optional parameter. When true, reads from the underlying DynamoDB table will use consistent reads. 
        /// Having consistent reads means that any read will be from the latest data in the DynamoDB table.
        /// However, using consistent reads requires more read capacity affecting the cost of the DynamoDB table.
        /// To reduce cost this property could be set to false but the application must be able to handle
        /// a delay after a set operation for the data to come back in a get operation.
        ///<para>
        /// Default is true.
        ///</para>
        /// </summary>
        public bool UseConsistentReads { get; set; } = true;

        /// <summary>
        /// Name of the TTL column when Table is created here. If this is not set a DescribeTimeToLive service call
        /// will be made to determine the partition key's name. To reduce startup time or avoid needing 
        /// permissions to DescribeTimeToLive this property should be set.
        /// <para>
        /// When a table is created by this library the TTL attribute is set to "ttl_date".
        /// </para>
        /// </summary>
        public string? TTLAttributeName { get; set; }

        /// <summary>
        /// Name of DynamoDB table's partion key. If this is not set 
        /// a DescribeTable service call will be made at startup to determine the partition key's name. To
        /// reduce startup time or avoid needing permissions to DescribeTable this property should be set.
        /// </summary>
        public string? PartitionKeyName { get; set; }

        /// <summary>
        /// Optional parameter. Prefix added to value of the partition key stored in DynamoDB.
        /// </summary>
        public string? PartitionKeyPrefix { get; set; }

        DynamoDBDistributedCacheOptions IOptions<DynamoDBDistributedCacheOptions>.Value
        {
            get { return this; }
        }
    }
}
