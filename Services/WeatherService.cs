using System.Globalization;
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

        // ---------------------------------------------------------
        // Reverse Geocoding → Resolve City Name
        // ---------------------------------------------------------
        private async Task<string> ResolveCityName(double lat, double lon)
        {
            string url =
                $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&localityLanguage=en";

            Console.WriteLine("🌍 Reverse geocoding URL: " + url);

            try
            {
                var response = await _httpClient.GetAsync(url);
                Console.WriteLine("Geo Status: " + response.StatusCode);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonNode.Parse(json)?.AsObject();

                var city = root?["city"]?.ToString()
                           ?? root?["locality"]?.ToString()
                           ?? root?["principalSubdivision"]?.ToString()
                           ?? "Unknown";

                return city;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ City lookup failed: " + ex.Message);
                return "Unknown";
            }
        }


        // ---------------------------------------------------------
        // WEATHER SUMMARY SERVICE
        // ---------------------------------------------------------
        public async Task<object> GetWeatherSummary(double lat, double lon, int hours = 6)
        {
            string url =
                $"{_baseUrl}?latitude={lat}&longitude={lon}" +
                "&hourly=temperature_2m,apparent_temperature,precipitation,weathercode," +
                "windspeed_10m,relative_humidity_2m" +
                "&daily=sunrise,sunset" +
                "&timezone=auto";

            Console.WriteLine($"🌍 Requesting: {url}");
            var response = await _httpClient.GetStringAsync(url);

            var root = JsonNode.Parse(response);
            if (root is JsonArray arr)
                root = arr[0];

            if (root is not JsonObject json)
                throw new Exception("Invalid JSON root object.");

            var hourly = json["hourly"]!.AsObject();
            var daily = json["daily"]!.AsObject();

            // LOCAL TIME conversion
            var times = hourly["time"]!.AsArray()
                .Select(t =>
                {
                    DateTime dt = DateTime.Parse(t!.ToString());
                    return DateTime.SpecifyKind(dt, DateTimeKind.Local);
                })
                .ToList();

            DateTime now = DateTime.Now;

            var indices = times
                .Select((t, i) => new { t, i })
                .Where(x => x.t >= now && x.t <= now.AddHours(hours))
                .Select(x => x.i)
                .ToList();

            if (!indices.Any())
                throw new Exception("No hourly data available for the given range.");

            var temps = indices.Select(i => (double)hourly["temperature_2m"]![i]!).ToList();
            var feels = indices.Select(i => (double)hourly["apparent_temperature"]![i]!).ToList();
            var winds = indices.Select(i => (double)hourly["windspeed_10m"]![i]!).ToList();
            var rains = indices.Select(i => (double)hourly["precipitation"]![i]!).ToList();
            var humidities = indices.Select(i => (double)hourly["relative_humidity_2m"]![i]!).ToList();
            var codes = indices.Select(i => (int)hourly["weathercode"]![i]!).ToList();

            // Stats
            double RoundHalf(double x) => Math.Round(x * 2, MidpointRounding.AwayFromZero) / 2;

            // Main condition
            string mainCondition = MapWeatherCode(
                codes.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key
            );

            // Determine city (backend)
            string city = await ResolveCityName(lat, lon);

            var result = new
            {
                location = new
                {
                    city = city,
                    latitude = lat,
                    longitude = lon
                },
                dateTimeStart = now.ToString("yyyy-MM-ddTHH:mm:ss"),
                intervalHours = hours,
                tempMin = RoundHalf(temps.Min()),
                tempMax = RoundHalf(temps.Max()),
                feelsLikeAvg = RoundHalf(feels.Average()),
                windAvg = Math.Round(winds.Average(), 1),
                humidityAvg = Math.Round(humidities.Average(), 1),
                rainChance = Math.Round((double)rains.Count(r => r > 0.2) / rains.Count * 100),
                mainCondition = mainCondition
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
