![.NET on AWS Banner](./logo.png ".NET on AWS")

# AWS .NET Distributed Cache Provider
AWS Dotnet Distributed Cache Provider provides an implmentation on IDistributedCache backed by AWS DynamoDB.

The library has the following dependencies
* [AWSSDK.DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2)
* [AWSSDK.Extensions.NETCore.Setup](https://www.nuget.org/packages/AWSSDK.Extensions.NETCore.Setup/)
* [Microsoft.Extensions.Caching.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Abstractions)
* [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions)
* [Microsoft.Extensions.Options](https://www.nuget.org/packages/Microsoft.Extensions.Options)


# Getting Started
.NET has an interface for distributed caching called [IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-6.0). This library provides an implementation that uses AWS DyanmoDB as the underlying datastore.

.NET's convention is to use Dependency Injection to provide services to different aspects of an application's logic. This library provides Extensions to assist the user in injecting this implementation of IDistributedCache as a service for other services to consume. 

This library also provides some configuration options to help fit this library into the customer's application

## Sample
For example, if you were building an application that requires the use of sessions in a distributed webapp, .NET's Session provider looks for an implementation of IDistributedCache to store the session data. We can direct the session service to use our distributed cache service using dependency Injection

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSDynamoDBDistributedCache("dynamo_sessions_cache_table");
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(90);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var app = builder.Build();
...
```
Where `dynamo_sessions_cache_table` is the name of the underlying table. 

There are a few configurable options that the user has at their discretion to use.
* TableName - string - The name of the table to be used as the underlying table
* CreateTableIfNotExists - boolean - If the table specified does not exist, should the library create the table
* * It should be noted that when this feature is used, it does not turn on Health Checks on the new DynamoDB Table. The user should strongly consider turning this on if the cache needs to be highly available in the user's application. See more [here](https://aws.amazon.com/builders-library/implementing-health-checks/).
* ConsistentReads - boolean - DynamoDB is a distributed eventually consistent database, which means that a given time, its not a guarantee that all the replications have the same data for a given item. The user has the option to require data reads be consistent across all replications to make sure it is the most up to date value, although at a performance hit. See more [Here](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html)
* TTLAttributeName - string - One of DynamoDB's features is Time To Live (TTL) where we can specify how long an item should remain in the database before it is deleted. IDistributedCache's [Set](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache.set?view=dotnet-plat-ext-6.0#microsoft-extensions-caching-distributed-idistributedcache-set(system-string-system-byte()-microsoft-extensions-caching-distributed-distributedcacheentryoptions)) function takes a [parameter](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.distributedcacheentryoptions?view=dotnet-plat-ext-6.0) specifiying this information. The user has the option to set the attribute name this information will stored under in the DynamoDB Table.

The options can be used in the following way
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSDynamoDBDistributedCache(options =>
{
    options.TableName = "dynamo_sessions_cache_table";
    options.CreateTableIfNotExists = true;
    options.ConsistentReads = true;
    options.TTLAttributeName = "foo"

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
It is important to note that only the `TableName` is a required parameter, the other three are optional. 

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