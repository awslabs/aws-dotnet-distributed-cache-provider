![.NET on AWS Banner](./logo.png ".NET on AWS")

# AWS .NET Distributed Cache Provider [![nuget](https://img.shields.io/nuget/v/AWS.AspNetCore.DistributedCacheProvider.svg) ![downloads](https://img.shields.io/nuget/dt/AWS.AspNetCore.DistributedCacheProvider.svg)](https://www.nuget.org/packages/AWS.AspNetCore.DistributedCacheProvider/)
The AWS .NET Distributed Cache Provider provides an implementation of the ASP.NET Core interface [IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed) backed by Amazon DynamoDB. A common use of an `IDistributedCache` implementation is to store ephemeral, non-critical [session state](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state?#session-state) data in ASP.NET Core applications.

# Getting Started
Install the [AWS.AspNetCore.DistributedCacheProvider](https://www.nuget.org/packages/AWS.AspNetCore.DistributedCacheProvider/) package from NuGet.

.NET uses [dependency injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) to provide services to different objects that rely on them. This library provides extensions to assist the user in injecting this implementation of `IDistributedCache` as a service for other objects to consume.

## Sample
For example, if you are building an application that requires the use of sessions in a distributed webapp, .NET's session state middleware looks for an implementation of `IDistributedCache` to store the session data. You can direct the session service to use the DynamoDB distributed cache implementation using dependency injection:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSDynamoDBDistributedCache(options =>
{
    options.TableName = "session_cache_table";
    options.PartitionKeyName = "id";
    options.TTLAttributeName = "cache_ttl";

});
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(90);
    options.Cookie.IsEssential = true;
});
var app = builder.Build();
...
```

For more information about .NET's session state middleware, see [this article](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/app-state) and specifically [this section](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/app-state#configure-session-state) regarding dependency injection.

## Configuration
Here are the available options to configure the `DynamoDBDistributedCache`:
* **TableName** (required) - string - The name of an existing table that will be used to store the cache data.
* **CreateTableIfNotExists** (optional) - boolean - If set to true during startup the library will check if the specified table exists. If the table does not exist a new table will be created using on demand provision throughput. This will require extra permissions to call the `CreateTable` and `UpdateTimeToLive` service operations. It is recommended to only set this to `true` in development environments. Production environments should create the table before deployment. This will allow production deployments to require fewer permissions and have a faster startup. **Default value is `false`**.
* **UseConsistentReads** (optional) - boolean - When `true`, reads from the underlying DynamoDB table will use consistent reads. Having consistent reads means that any read will be from the latest data in the DynamoDB table. However, using consistent reads requires more read capacity affecting the cost of the DynamoDB table. To reduce cost this property could be set to `false` but the application must be able to handle a delay after a set operation for the data to come back in a get operation. **Default value is true**. See more [here](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html).
* **PartitionKeyName** (optional) - string - Name of DynamoDB table's partition key. If this is not set a `DescribeTable` service call will be made at startup to determine the partition key's name. To reduce startup time and avoid needing permissions to `DescribeTable` this property should be set.
* **PartitionKeyPrefix** (optional) - string - Prefix added to value of the partition key stored in DynamoDB.
* **TTLAttributeName** (optional) - string - DynamoDB's [Time To Live (TTL) feature](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/TTL.html) is used for removing expired cache items from the table. This option specifies the attribute name that will be used to store the TTL timestamp. If this is not set, a `DescribeTimeToLive` service call will be made to determine the TTL attribute's name. To reduce startup time and avoid needing permissions to `DescribeTimeToLive` this property should be set.


The options can be used in the following way:
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSDynamoDBDistributedCache(options =>
{
    options.TableName = "session_cache_table";
    options.CreateTableIfNotExists = true;
    options.UseConsistentReads = true;
    options.PartitionKeyName = "id";
    options.TTLAttributeName = "cache_ttl"

});
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(90);
    options.Cookie.IsEssential = true;
});
var app = builder.Build();
...
```

## Table Considerations
The cache provider will use the table specified by `TableName` if it already exists. The table must use a non-composite partition key of type string, otherwise an exception will be thrown during the first cache operation. No settings will be modified on existing tables.

For production environments, we recommend setting `CreateTableIfNotExists` to `false` and providing both a `PartitionKeyName` and `TTLAttributeName`. This minimizes the required permissions and additional control plane calls during the first cache operation. It is recommended to only set `CreateTableIfNotExists` to `true` in development environments.

When `CreateTableIfNotExists` is set to `true` and if the table specified by `TableName` does not already exist, the cache provider will create the table during the first cache operation with the following default settings:
* The [on-demand](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadWriteCapacityMode.html#HowItWorks.OnDemand)  capacity mode
* [Time to Live](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/TTL.html) enabled
* Encryption using an [AWS KMS key owned and managed by DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/encryption.howitworks.html#ddb-owned). If you would like to use an AWS managed or customer managed key, you can update the table after it is created or provide your own table. See [Managing encrypted tables](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/encryption.tutorial.html) in the Amazon DynamoDB Developer Guide.

# Permissions
To use the cache provider with an _existing_ DynamoDB table and a provided `PartitionKeyName` and `TTLAttributeName`, the following permissions are required:
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "dynamodb:PutItem",
                "dynamodb:DeleteItem",
                "dynamodb:GetItem",
                "dynamodb:UpdateItem"
            ],
            "Resource": "arn:aws:dynamodb:*:<account>:table/<table>"
        }
    ]
}
```

If configured to use an existing table but  `PartitionKeyName` and `TTLAttributeName` are not specified, then the following permissions are required _in addition to_ those specified above:
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "dynamodb:DescribeTable",
                "dynamodb:DescribeTimeToLive"
            ],
            "Resource": "arn:aws:dynamodb:*:<account>:table/<table>"
        }
    ]
}
```

If the cache provider is configured to create a table via the `CreateTableIfNotExists` option described above, then the following permissions are required _in addition to_ the minimal permissions specified above:
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "dynamodb:CreateTable",
                "dynamodb:UpdateTimeToLive"
            ],
            "Resource": "arn:aws:dynamodb:*:<account>:table/<table>"
        }
    ]
}
```

The `LeadingKeys` condition can optionally be used to restrict access to only cache entry items in the DynamoDB table. The prefix can be configured via the `PartitionKeyPrefix` option described above, with the default value `"dc"`. See [Using IAM policy conditions for fine-grained access control](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/specifying-conditions.html) for more information.
```
    "Resource": "arn:aws:dynamodb:*:<acount>:table/<table>",
    "Condition": {
        "ForAllValues:StringLike": {
            "dynamodb:LeadingKeys": "dc:*"
        }
    }
```
# Time to Live
`IDistributedCache` exposes several options for for expiring entries from the cache on its [`DistributedCacheEntryOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.distributedcacheentryoptions). If using the `IDistributedCache` to store session state for an ASP.NET Core application, [`SessionOptions.IdleTimeout`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.sessionoptions.idletimeout) controls the expiration for cache entries.

The AWS .NET Distributed Cache Provider relies on DynamoDB's [Time to Live (TTL) feature](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/TTL.html) to delete expired items from the table. TTL will be enabled automatically when tables are created via `CreateTableIfNotExists`. If you provide an existing table and TTL is **not enabled, expired entries will not be deleted** and a warning will be logged during the first cache operation.

Because DynamoDB's TTL feature can take up to a few days to delete expired items, the cache provider filters out items that have expired but not been deleted yet on the client side.

# Auditing
By default CloudTrail is enabled on AWS accounts when they are created and will capture control plane events for DynamoDB.

To capture data plane events, refer to [Logging data events](https://docs.aws.amazon.com/awscloudtrail/latest/userguide/logging-data-events-with-cloudtrail.html) in the AWS CloudTrail User Guide.

# Dependencies

The library has the following dependencies
* [AWSSDK.DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2)
* [AWSSDK.Extensions.NETCore.Setup](https://www.nuget.org/packages/AWSSDK.Extensions.NETCore.Setup/)
* [Microsoft.Extensions.Caching.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Abstractions)
* [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions)
* [Microsoft.Extensions.Options](https://www.nuget.org/packages/Microsoft.Extensions.Options)


# Getting Help

We use the [GitHub issues](https://github.com/aws/aws-dotnet-distributed-cache-provider/issues) for tracking bugs and feature requests and have limited bandwidth to address them.

If you think you may have found a bug, please open an [issue](https://github.com/aws/aws-dotnet-distributed-cache-provider/issues/new).

# Contributing

We welcome community contributions and pull requests. See
[CONTRIBUTING.md](./CONTRIBUTING.md) for information on how to set up a development environment and submit code.

# Additional Resources

[AWS .NET GitHub Home Page](https://github.com/aws/dotnet)  
GitHub home for .NET development on AWS. You'll find libraries, tools, and resources to help you build .NET applications and services on AWS.

[AWS Developer Center - Explore .NET on AWS](https://aws.amazon.com/developer/language/net/)  
Find all the .NET code samples, step-by-step guides, videos, blog content, tools, and information about live events that you need in one place.

[AWS Developer Blog - .NET](https://aws.amazon.com/blogs/developer/category/programing-language/dot-net/)  
Come see what .NET developers at AWS are up to!  Learn about new .NET software announcements, guides, and how-to's.

[@dotnetonaws](https://twitter.com/dotnetonaws)
Follow us on twitter!

# Security

The AWS .NET Distributed Cache Provider relies on the [AWS SDK for .NET](https://github.com/aws/aws-sdk-net) for communicating with AWS. Refer to the [security section](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/security.html) in the [AWS SDK for .NET Developer Guide](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/welcome.html) for more information.

If you discover a potential security issue, refer to the [security policy](https://github.com/awslabs/aws-dotnet-distributed-cache-provider/security/policy) for reporting information.

# License

Libraries in this repository are licensed under the Apache 2.0 License.

See [LICENSE](./LICENSE) and [NOTICE](./NOTICE) for more information.
