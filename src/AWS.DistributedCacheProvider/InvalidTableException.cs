// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace AWS.DistributedCacheProvider
{
    [Serializable]
    public  class InvalidTableException : Exception
    {
        public InvalidTableException() { }

        public InvalidTableException(string message) : base(message) { }

    }
}
