// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public string? tablename { get; set; }

        /// <summary>
        /// Optional parameter. Enables Time To Live (TTL) feature on the DynamoDB Table when creating. Default is false
        /// </summary>
        public bool enableTtl { get; set; }

        /// <summary>
        /// Optional parameter. If the table tablename does not exist, create the table.  Default is false
        /// </summary>
        public bool createTableIfNotExists { get; set; }

        /// <summary>
        /// Optional parameter. Must reads from the underlying Table be consistent  Default is false
        /// </summary>
        public bool consistentReads { get; set; }

        /// <summary>
        /// Optional Parameter. Attribute name that the cache will store the TTL information under. If not set, a default value will be used.
        /// </summary>
        public string? ttlAttributeName { get; set; }

        /// <summary>
        /// Optional Parameter. Credentials to be used to load or create the DynamoDB Table
        /// </summary>
        public AWSCredentials? credentials { get; set; }

        /// <summary>
        /// Optional Parameter. Configuration for a DynamoDB Client
        /// </summary>
        public AmazonDynamoDBConfig? dynamoConfig { get; set; }

        public DynamoDBDistributedCacheOptions Value
        {
            get { return this; }
        }
    }
}
