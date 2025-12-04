using System.Text.Json.Nodes;

namespace StyleCast.Backend.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public WeatherService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _baseUrl = config["OpenMeteo:BaseUrl"] ?? "https://api.open-meteo.com/v1/forecast";
        }

        public async Task<object> GetWeatherSummary(double lat, double lon, int hours = 6)
        {
            var url = $"{_baseUrl}?latitude={lat}&longitude={lon}" +
                      "&hourly=temperature_2m,apparent_temperature,precipitation,weathercode," +
                      "windspeed_10m,relative_humidity_2m" +
                      "&daily=sunrise,sunset" +
                      "&timezone=auto";

            Console.WriteLine($"🌍 Requesting: {url}");

            var response = await _httpClient.GetStringAsync(url);
            Console.WriteLine("🌐 RAW RESPONSE:");
            Console.WriteLine(response);

            //handle possible array root element
            var root = JsonNode.Parse(response);
            if (root is JsonArray arr)
                root = arr[0];
            if (root is not JsonObject json)
                throw new Exception("Invalid JSON structure — expected object at root.");

            var hourly = json["hourly"]!.AsObject();
            var daily = json["daily"]?.AsObject();

            var times = hourly["time"]!.AsArray().Select(t => DateTime.Parse(t!.ToString())).ToList();
            var now = DateTime.UtcNow;

            var indices = times
                .Select((t, i) => new { t, i })
                .Where(x => x.t >= now && x.t <= now.AddHours(hours))
                .Select(x => x.i)
                .ToList();

            if (!indices.Any())
                throw new Exception("No hourly data available for the given range.");

            // xtract hourly data arrays
            var temps = indices.Select(i => (double)hourly["temperature_2m"]![i]!).ToList();
            var feels = indices.Select(i => (double)hourly["apparent_temperature"]![i]!).ToList();
            var winds = indices.Select(i => (double)hourly["windspeed_10m"]![i]!).ToList();
            var rains = indices.Select(i => (double)hourly["precipitation"]![i]!).ToList();
            var humidities = indices.Select(i => (double)hourly["relative_humidity_2m"]![i]!).ToList();
            var codes = indices.Select(i => (int)hourly["weathercode"]![i]!).ToList();

            //calculate statistics
            var tempMin = temps.Min();
            var tempMax = temps.Max();
            var feelsAvg = feels.Average();
            var windAvg = winds.Average();
            var humidityAvg = humidities.Average();

            //simpler rainChance: percent of hours with measurable precipitation (>0.2 mm)
            var rainChance = Math.Round((double)rains.Count(r => r > 0.2) / rains.Count * 100, 0);


            //determine main condition
            var mainCondition = MapWeatherCode(
                codes.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key
            );

            //ddjust feels like if nighttime (after sunset or before sunrise)
            if (daily != null)
            {
                try
                {
                    var sunrise = DateTime.Parse(daily["sunrise"]!.AsArray().First()!.ToString());
                    var sunset = DateTime.Parse(daily["sunset"]!.AsArray().First()!.ToString());
                    if (now < sunrise || now > sunset)
                        feelsAvg -= 1.5; // cuz it's usually colder
                }
                catch { /* fallback safe */ }
            }

            //round to 0.5 °C steps for human readability
            double RoundHalf(double x) => Math.Round(x * 2, MidpointRounding.AwayFromZero) / 2;

            var result = new
            {
                location = new
                {
                    city = "Bucharest",     // replace later with actual detected city
                    latitude = lat,
                    longitude = lon
                },
                dateTimeStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                intervalHours = hours,
                tempMin = RoundHalf(tempMin),
                tempMax = RoundHalf(tempMax),
                feelsLikeAvg = RoundHalf(feelsAvg),
                windAvg = Math.Round(windAvg, 1),
                humidityAvg = Math.Round(humidityAvg, 1),
                rainChance,
                mainCondition
            };

            return result;
        }

        private string MapWeatherCode(int code)
        {
            return code switch
            {
                0 => "Clear",
                1 or 2 => "Partly Cloudy",
                3 => "Overcast",
                45 or 48 => "Fog",
                51 or 53 or 55 => "Drizzle",
                61 or 63 or 65 => "Rain",
                71 or 73 or 75 => "Snow",
                80 or 81 or 82 => "Showers",
                95 or 96 or 99 => "Thunderstorm",
                _ => "Unknown"
            };
        }
    }
}
