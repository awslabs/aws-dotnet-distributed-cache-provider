![.NET on AWS Banner](./logo.png ".NET on AWS")

# AWS .NET Distributed Cache Provider
AWS Dotnet Distributed Cache Provider provides an implementation of the ASP.NET Core interface [IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed) backed by AWS DynamoDB. The IDistributedCache interface is used in ASP.NET Core applications to provide a distributed cache store as well as the backing store ASP.NET Core session state.


# Getting Started
.NET has an interface for distributed caching called [IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed). This library provides an implementation that uses AWS DynamoDB as the underlying datastore.

.NET's convention is to use Dependency Injection to provide services to different aspects of an application's logic. This library provides extensions to assist the user in injecting this implementation of IDistributedCache as a service for other services to consume. 

This library also provides some configuration options to help fit this library into your application.

## Sample
For example, if you are building an application that requires the use of sessions in a distributed webapp, .NET's session state middleware looks for an implementation of IDistributedCache to store the session data. We can direct the session service to use our distributed cache implementation using dependency Injection

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSDynamoDBDistributedCache(options =>
{
    options.TableName = "existing_dynamo_sessions_cache_table";
    options.PartitionKeyName = "id";
    options.TTLAttributeName = "ttl_date_";

});
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(90);
    options.Cookie.IsEssential = true;
});
var app = builder.Build();
...
```
Where `existing_dynamo_sessions_cache_table` is the name of the underlying table. 

For more information about .NET's session state middleware, see [this article](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/app-state) and specifically [this section](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/app-state#configure-session-state) regarding dependency injection.

Here are the available options to configure the `DynamoDBDistributedCache`.
* **TableName** - string - required - The name of an existing table that will be used to store the cache data.
* **CreateTableIfNotExists** - boolean - optional - If set to true during startup the library will check if the specified table exists. If the
table does not exist a new table will be created using on demand provision throughput.
This will require extra permissions to call DescribeTable, CreateTable and UpdateTimeToLive service operations. It is recommended to only set to true in
development environments. Production environments should create the table before deployment. This will 
allow production deployments to have less permissions and have faster startup. **Default value is false**.
     * It should be noted that when DynamoDBDistributedCache creates a table, it does not turn on Health Checks on the new DynamoDB Table. We strongly advise turning this on if the cache needs to be highly available in the your application. See more [here](https://aws.amazon.com/builders-library/implementing-health-checks/).
* **UseConsistentReads** - boolean - optional - When true, reads from the underlying DynamoDB table will use consistent reads. 
Having consistent reads means that any read will be from the latest data in the DynamoDB table.
However, using consistent reads requires more read capacity affecting the cost of the DynamoDB table.
To reduce cost this property could be set to false but the application must be able to handle
a delay after a set operation for the data to come back in a get operation. **Default value is true**. See more [Here](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html).
* **PartitionKeyName** - string - optional - Name of DynamoDB table's partion key. If this is not set 
a DescribeTable service call will be made at startup to determine the partition key's name. To
reduce startup time or avoid needing permissions to DescribeTable this property should be set.
* **PartitionKeyPrefix** - string - optional - Prefix added to value of the partition key stored in DynamoDB.
* **TTLAttributeName** - string - optional - DynamoDB's Time To Live (TTL) feature is relied on for removing expired cached items from the table. This option configures
the provider for setting the attribute for the DynamoDB item that the TTL is configured for on the table. If this is not set a DescribeTimeToLive service call
will be made to determine the partition key's name. To reduce startup time or avoid needing permissions to DescribeTimeToLive this property should be set.


The options can be used in the following way:
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSDynamoDBDistributedCache(options =>
{
    options.TableName = "dynamo_sessions_cache_table";
    options.CreateTableIfNotExists = true;
    options.ConsistentReads = true;
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
