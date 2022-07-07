// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWS.DistributedCacheProvider
{
    [Serializable]
    public  class InvalidTableException : Exception
    {
        public InvalidTableException() { }

        public InvalidTableException(string message)
            : base(message) { }

    }
}
