// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace AWS.DistributedCacheProvider
{
    /// <summary>
    /// An Exception that is thrown when the Configuration points to a DynamoDB table that is not suitable for a cache
    /// </summary>
    [Serializable]
    public class InvalidTableException : Exception
    {
        public InvalidTableException() { }

        public InvalidTableException(string message) : base(message) { }

    }
}
