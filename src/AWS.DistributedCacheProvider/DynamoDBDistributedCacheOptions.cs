// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Options;

namespace AWS.DistributedCacheProvider
{
    /// <summary>
    /// Configurable parameters for DynamoDBDistributedCacheOptions
    /// </summary>
    public class DynamoDBDistributedCacheOptions : IOptions<DynamoDBDistributedCacheOptions>
    {
        /// <summery>
        /// Required parameter. The name of the backing DynamoDB Table. Cannot be Null or Empty
        /// </summery>
        public string TableName { get; set; }

        /// <summary>
        /// Optional parameter. Enables Time To Live (TTL) feature on the DynamoDB Table when creating. Default is false
        /// </summary>
        public bool EnableTtl { get; set; }

        /// <summary>
        /// Optional parameter. If the table tablename does not exist, create the table.  Default is false
        /// </summary>
        public bool CreateTableIfNotExists { get; set; }

        /// <summary>
        /// Optional parameter. Must reads from the underlying Table be consistent  Default is false
        /// </summary>
        public bool ConsistentReads { get; set; }

        public string? TTLAttributeName { get; set; }

        /// <summary>
        /// Optional Parameter. Credentials to be used to load or create the DynamoDB Table
        /// </summary>
        public AWSCredentials? Credentials { get; set; }

        /// <summary>
        /// Optional Parameter. Configuration for a DynamoDB Client
        /// </summary>
        public AmazonDynamoDBConfig? DynamoConfig { get; set; }

        public DynamoDBDistributedCacheOptions Value
        {
            get { return this; }
        }
    }
}
