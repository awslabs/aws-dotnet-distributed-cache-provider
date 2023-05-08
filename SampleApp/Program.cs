// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace SampleApp;

public class Program
{
    /// <summary>
    /// Session key for the cached forecast data
    /// </summary>
    private const string ForecastCacheKey = "forecast";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Add session state support
        builder.Services.AddSession(options =>
        {
            // The following option will cache weather forecasts for 30 seconds.
            // Reloading a cached forecast will reset this timer. If a cached forecast
            // is not requested for 30 seconds it should expire and a new
            // forecast will be generated.
            options.IdleTimeout = TimeSpan.FromSeconds(30);
            options.Cookie.IsEssential = true;
        });

        // Add DynamoDB distributed cache for sessions
        builder.Services.AddAWSDynamoDBDistributedCache(options =>
        {
            options.TableName = "weather-sessions-cache";
            options.UseConsistentReads = true;
            options.PartitionKeyName = "id";
            options.TTLAttributeName = "cache_ttl";

            // The following option will create a table with the above name if it doesn't
            // already exist in the account corresponding to the detected credentials.
            //
            // For production we recommend setting this to false and using a preexisting
            // table to reduce the startup time and require fewer permissions.
            options.CreateTableIfNotExists = true;
        });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.UseSession();

        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/", () => "Welcome to the Amazon DynamoDB distributed caching sample! GET /weatherforecast to test the caching.");

        app.MapGet("/weatherforecast", (HttpContext httpContext) =>
        {
            // First check if there is a cached forecast stored in the session state
            if (httpContext.Session.TryGetValue(ForecastCacheKey, out var serializedForecast))
            {
                // If there is, deserialize it and return it
                var cachedForecast = JsonSerializer.Deserialize<WeatherForecast>(serializedForecast);

                if (cachedForecast != null)
                {
                    return cachedForecast;
                }
            }

            // Otherwise if we made it here, generate a new forecast
            var forecast = new WeatherForecast
            {
                DateForecastWasGenerated = DateTimeOffset.UtcNow,
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = summaries[Random.Shared.Next(summaries.Length)]
            };

            // Save the new forecast in the session state and then return it
            httpContext.Session.Set(ForecastCacheKey, JsonSerializer.SerializeToUtf8Bytes(forecast));
            return forecast;
        })
        .WithName("GetWeatherForecast");

        app.Run();
    }
}
