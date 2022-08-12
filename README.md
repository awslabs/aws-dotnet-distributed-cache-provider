![.NET on AWS Banner](./logo.png ".NET on AWS")

# AWS .NET Distributed Cache Provider

[![nuget](https://img.shields.io/nuget/v/Amazon.Extensions.Configuration.SystemsManager.svg)](https://www.nuget.org/packages/Amazon.Extensions.Configuration.SystemsManager/)
^Need to change this to this project.

AWS Dotnet Distributed Cache Provider provides an implmentation on IDistributedCache backed by AWS DynamoDB.

The library has the following dependencies
* AWSSDK.DynamoDBv2
* AWSSDK.Extensions.NETCore.Setup
* Microsoft.Extensions.Caching.Abstractions
* Microsoft.Extensions.DependencyInjection.Abstractions
* Microsoft.Extensions.Options

# Getting Started
.NET uses dependency injection to resolve implementations for interfaces at runtime. This library injects its implementation of IDistributedCache into the dependency injection framework as a service that can be called. To use the cache provider in this way, in `Program.cs` 

Be sure to:

* Change the title in this README
* Edit your repository description on GitHub

## Security

See [CONTRIBUTING](CONTRIBUTING.md#security-issue-notifications) for more information.

## License

This project is licensed under the Apache-2.0 License.

