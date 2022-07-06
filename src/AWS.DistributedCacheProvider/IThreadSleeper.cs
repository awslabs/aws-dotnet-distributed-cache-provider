// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace AWS.DistributedCacheProvider
{
    /// <summary>
    /// Interface for abstracting Thread Sleep. Needed for removing Sleep operations in Unit tests.
    /// </summary>
    public interface IThreadSleeper
    {
        /// <summary>
        /// Sleep the current Thread for <paramref name="milliseconds"/>
        /// </summary>
        /// <param name="milliseconds"></param>
        void Sleep(int milliseconds);
    }
}
