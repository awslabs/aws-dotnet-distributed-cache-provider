using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private const string listStr = "forecast_List";
        private const string nextIdStr = "Next_ID";

        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return GetListOrGenerate();
        }

        private List<WeatherForecast> GetListOrGenerate()
        {
            var forecastString = HttpContext.Session.GetString(listStr) ?? "";
            if (!string.IsNullOrEmpty(forecastString))
            {
                var forecastList = new List<WeatherForecast>();
                var parts = forecastString.Split(new string[] { "," }, StringSplitOptions.None);
                foreach (var part in parts)
                {
                    if (String.IsNullOrEmpty(part))
                    {
                        continue;
                    }
                    else
                    {
                        forecastList.Add(WeatherForecast.ForcastFromString(part));
                    }
                }
                return forecastList;
            }
            else
            {
                var forecastList = new List<WeatherForecast>();
                foreach(var val in Enumerable.Range(1, 5))
                {
                    forecastList.Add(new WeatherForecast
                    {
                        Date = DateTimeOffset.Now.AddDays(val),
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                    });
                }
                StoreList(forecastList);
                return forecastList;
            }
        }

        private void StoreList(List<WeatherForecast> listToStore)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < listToStore.Count; i++)
            {
                sb.Append(listToStore[i].Serialize());
                if (i != listToStore.Count)//Not the last entry
                {
                    _ = sb.Append(",");
                }
            }
            HttpContext.Session.SetString(listStr, sb.ToString());
        }
    }
}
