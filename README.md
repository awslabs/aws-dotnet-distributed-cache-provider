![.NET on AWS Banner](./logo.png ".NET on AWS")

# AWS .NET Distributed Cache Provider
AWS Dotnet Distributed Cache Provider provides an implementation of IDistributedCache backed by AWS DynamoDB.

# Getting Started
.NET has an interface for distributed caching called [IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-6.0). This library provides an implementation that uses AWS DynamoDB as the underlying datastore.

.NET's convention is to use Dependency Injection to provide services to different aspects of an application's logic. This library provides extensions to assist the user in injecting this implementation of IDistributedCache as a service for other services to consume. 

This library also provides some configuration options to help fit this library into your application.

## Sample
For example, if you are building an application that requires the use of sessions in a distributed webapp, .NET's session state middleware looks for an implementation of IDistributedCache to store the session data. We can direct the session service to use our distributed cache implementation using dependency Injection

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSDynamoDBDistributedCache("existing_dynamo_sessions_cache_table");
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(90);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();
...
```
Where `existing_dynamo_sessions_cache_table` is the name of the underlying table. 

For more information about .NET's session state middleware, see [this article](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/app-state?view=aspnetcore-6.0) and specifically [this section](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/app-state?view=aspnetcore-6.0#configure-session-state) regarding dependency injection.

Here are the available options to configure the `DynamoDBDistributedCache`.
* **TableName** - string - required - The name of an existing table that will be used to store the cache data.
* **CreateTableIfNotExists** - boolean - optional - If the table specified does not exist, should the library create the table
     * It should be noted that when DynamoDBDistributedCache creates a table, it does not turn on Health Checks on the new DynamoDB Table. We strongly advise turning this on if the cache needs to be highly available in the your application. See more [here](https://aws.amazon.com/builders-library/implementing-health-checks/).
* **ConsistentReads** - boolean - optional - By default, DynamoDB offers "eventually consistent reads" which may not reflect the results of a recently completed write operation. Set this to true to enforce "strongly consistent reads", which will return the most up-to-date data. Enabling strongly consistent reads may have higher latency and use more throughput capacity. See more [Here](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html).
* **TTLAttributeName** - string - optional - One of DynamoDB's features is Time To Live (TTL) where you can specify how long an item should remain in the database before it is deleted. IDistributedCache's [Set](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache.set?view=dotnet-plat-ext-6.0#microsoft-extensions-caching-distributed-idistributedcache-set) function takes a [DistributedCacheEntryOptions parameter](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.distributedcacheentryoptions?view=dotnet-plat-ext-6.0) specifying this information. You have the option to set the attribute name this information will stored under in the DynamoDB Table.

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
    options.Cookie.HttpOnly = true;
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

# License

Libraries in this repository are licensed under the Apache 2.0 License.

See [LICENSE](./LICENSE) and [NOTICE](./NOTICE) for more information.