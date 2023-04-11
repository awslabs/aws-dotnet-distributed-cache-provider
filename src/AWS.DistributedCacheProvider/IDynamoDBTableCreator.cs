// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;

namespace AWS.DistributedCacheProvider
{
    public interface IDynamoDBTableCreator
    {
        /// <summary>
        /// Tests first to see if Table <paramref name="tableName"/>" exists.
        /// If it does exist, check to see if the table is valid to serve as a cache.
        /// Requirements are that the table contain a non-composite Hash key of type String
        /// If the table does not exist, and <paramref name="create"/> is set to true, then create the table
        /// When creating a table, TTL is turned on using the <paramref name="TtlAttribute"/> name for the TTL column.
        /// If <paramref name="TtlAttribute"/> is not set, a default value will be used.
        /// </summary>
        /// <param name="client">DynamoDB client.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="create">Create the table if it does not exist.</param>
        /// <param name="TtlAttribute">When turning on TTL, what column should specifically be used? If left null, a default will be used.</param>
        /// <param name="partitionKeyAttribute">When creating a table, what should the partition key attribute name be? If left null, a default will be used.</param>
        /// <returns>The attribute name of the Table's partition key</returns>
        public Task<string> CreateTableIfNotExistsAsync(IAmazonDynamoDB client, string tableName, bool create, string TtlAttribute, string partitionKeyAttribute);

        /// <summary>
        /// Returns the TTL attribute column for this table.
        /// </summary>
        /// <param name="client">DynamoDB client.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <returns>The name of the TTL column for this table</returns>
        public Task<string> GetTTLColumnAsync(IAmazonDynamoDB client, string tableName);
    }
}
