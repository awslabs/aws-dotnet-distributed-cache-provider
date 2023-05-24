// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace AWS.DistributedCacheProvider
{
    /// <summary>
    /// An Exception that acts as a wrapper for any exception thrown during interactions with DynamoDB
    /// </summary>
    [Serializable]
    public class DynamoDBDistributedCacheException : Exception
    {
        public DynamoDBDistributedCacheException() { }

        public DynamoDBDistributedCacheException(string message) : base(message) { }

        public DynamoDBDistributedCacheException(string message, Exception innerException) : base(message, innerException) { }
    }
}
