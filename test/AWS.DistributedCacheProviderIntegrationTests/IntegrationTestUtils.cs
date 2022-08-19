// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace AWS.DistributedCacheProviderIntegrationTests
{
    public class IntegrationTestUtils
    {
        /// <summary>
        /// Concatenates the calling method's namespace, class name, method name and the UTC timestamp in
        /// milliseconds. To be used when generating a DynamoDB Table that needs a name that can be traced to which test
        /// generated the table should it not be deleted in the test directly. The UTC timestamp is so that multiple
        /// people can run the same test on the same account at the same time and hopefully not have a conflict.
        /// </summary>
        /// <returns>The calling methods information in "{namespace}-{class name}-{method name}-{UTC now}" format.</returns>
        public static string GetFullTestName()
        {
            //This method is being called from Integration tests. The methods being used here are not returning null.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            var base_method = new StackTrace().GetFrame(1).GetMethod().ReflectedType;
            var name_space = base_method.Namespace;
            var clazz = base_method.DeclaringType.Name;
            var method_name = base_method.Name.Split('<', '>')[1];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            var fullName = $"{name_space}-{clazz}-{method_name}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            return fullName;
        }
    }
}
