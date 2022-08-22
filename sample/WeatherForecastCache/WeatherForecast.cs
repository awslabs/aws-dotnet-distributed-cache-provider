namespace WebApplication1
{
    public class WeatherForecast
    {
        public DateTimeOffset Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string? Summary { get; set; }

        public WeatherForecast() { }

        public static WeatherForecast ForcastFromString(string str)
        {
            var parts = str.Split(new string[] { "][" }, StringSplitOptions.None);
            return new WeatherForecast
            {
                //need to remove the opening '[' from the first split string
                Date = DateTimeOffset.Parse(parts[0].Substring(1)),
                TemperatureC = int.Parse(parts[1]),
                //need to remove the ending ']' from the last split string
                Summary = parts[2].Substring(0, parts[2].Length - 1)
            };
        }

        public string Serialize()
        {
            return $"[{Date:O}][{TemperatureC}][{Summary}]";
        }
    }
}
