# AWS .NET Distributed Cache Provider Sample App

This sample is an ASP.NET Core minimal API that returns a weather forecast.

A GET request to `/weatherforecast` will initially return a random weather forecast. 

The generated forecast will be cached for the current user's session with an `IdleTimeout` of 30 seconds. Subsequent requests should return the cached forecast and reset the timeout. 

If a request is not made for 30 seconds, the cached forecast should expire. A subsequent request should generate a new forecast.

The cached data is stored in a DynamoDB table named `weather-sessions-cache`. This table will be created if it does not exist.

**Note:** Running this sample may create a DynamoDB table using the `default` or environmental AWS credentials. Since AWS resources are created and used during the running of this sample, charges may occur.
