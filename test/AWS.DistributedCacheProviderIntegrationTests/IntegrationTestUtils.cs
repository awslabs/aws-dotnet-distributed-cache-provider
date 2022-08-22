// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var baseMethod = new StackTrace().GetFrame(1).GetMethod().ReflectedType;
            var nameSpace = baseMethod.Namespace;
            var className = baseMethod.DeclaringType.Name;
            var methodName = baseMethod.Name.Split('<', '>')[1];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            var fullName = $"{nameSpace}-{className}-{methodName}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            //DynamoDB Table name cannot have special chars
            var filteredName = Regex.Replace(fullName, @"[^0-9a-zA-Z]+", "");
            //DynamoDB Table name length must be between 3 and 255 chars
            if(filteredName.Length > 255)
            {
                return filteredName.Substring(filteredName.Length - 255, filteredName.Length);
            }
            //DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() returns a string that is longer than 3 chars. No need to check that case.
            else
            {
                return filteredName;
            }
        }
    }
}
