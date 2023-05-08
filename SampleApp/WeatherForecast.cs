// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace SampleApp;

public class WeatherForecast
{
    /// <summary>
    /// The instant the forecast was generated, which may be in the past if it was cached
    /// </summary>
    public DateTimeOffset DateForecastWasGenerated { get; set; }

    /// <summary>
    /// Forecasted temperature in Celsius
    /// </summary>
    public int TemperatureC { get; set; }

    /// <summary>
    /// Forecasted temperature in Fahrenheit
    /// </summary>
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    /// <summary>
    /// Friendly description of the forecast
    /// </summary>
    public string? Summary { get; set; }
}
