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
            _baseUrl = config["OpenMeteo:BaseUrl"] 
                ?? "https://api.open-meteo.com/v1/forecast";
        }

        // -----------------------------
        // 🔍 Reverse Geocoding (Get City)
        // -----------------------------
        private async Task<string> ResolveCityName(double lat, double lon)
        {
            try
            {
                string url =
                    $"https://geocoding-api.open-meteo.com/v1/reverse?latitude={lat:F4}&longitude={lon:F4}&count=1";

                var response = await _httpClient.GetStringAsync(url);
                var json = JsonNode.Parse(response)?.AsObject();
                var results = json?["results"]?.AsArray();

                if (results != null && results.Count > 0)
                    return results[0]!["name"]!.ToString();
            }
            catch { }

            return "Unknown Location";
        }

        // -----------------------------
        // 🌡️ CURRENT WEATHER BUILDER
        // -----------------------------
        private object BuildCurrentWeather(JsonObject json, double lat, double lon, string city)
        {
            var hourly = json["hourly"]!.AsObject();
            var timezone = json["timezone"]!.ToString();

            var times = hourly["time"]!.AsArray()
                .Select(t => DateTime.Parse(t!.ToString()))
                .ToList();

            // Convert ALL hourly timestamps to the local timezone
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            // pick the closest future hour
            int idx = times.FindIndex(t => t >= nowLocal);
            if (idx == -1) idx = times.Count - 1; // fallback

            double temp = (double)hourly["temperature_2m"]![idx]!;
            double feels = (double)hourly["apparent_temperature"]![idx]!;
            int code = (int)hourly["weathercode"]![idx]!;
            double humidity = (double)hourly["relative_humidity_2m"]![idx]!;
            double wind = (double)hourly["windspeed_10m"]![idx]!;
            double precipitation = (double)hourly["precipitation"]![idx]!;

            return new
            {
                location = new { city, latitude = lat, longitude = lon },
                dateTimeStart = nowLocal.ToString("yyyy-MM-dd HH:mm"),
                intervalHours = 1,
                tempMin = temp,
                tempMax = temp,
                feelsLikeAvg = feels,
                windAvg = wind,
                humidityAvg = humidity,
                rainChance = precipitation > 0.2 ? 100 : 0,
                mainCondition = MapWeatherCode(code)
            };
        }

        // ---------------------------------------------------
        // 🌤️ MAIN SERVICE: CURRENT or INTERVAL FORECAST
        // ---------------------------------------------------
        public async Task<object> GetWeatherSummary(double lat, double lon, int hours = 6)
        {
            var url = $"{_baseUrl}?latitude={lat}&longitude={lon}" +
                      "&hourly=temperature_2m,apparent_temperature,precipitation,weathercode," +
                      "windspeed_10m,relative_humidity_2m" +
                      "&daily=sunrise,sunset" +
                      "&timezone=auto";

            var response = await _httpClient.GetStringAsync(url);
            var root = JsonNode.Parse(response);

            if (root is JsonArray arr)
                root = arr[0];

            var json = root!.AsObject();

            // --------------------------
            // 🌍 Resolve City Dynamically
            // --------------------------
            string cityName = await ResolveCityName(lat, lon);

            // --------------------------
            // 🔥 If hours == 1 => CURRENT weather
            // --------------------------
            if (hours == 1)
                return BuildCurrentWeather(json, lat, lon, cityName);


            // ---------------------------------------------------
            // 🌦️ FORECAST MODE (3h / 6h / 9h / 12h)
            // ---------------------------------------------------
            var hourly = json["hourly"]!.AsObject();

            var timezone = json["timezone"]!.ToString();
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);

            // Convert local UTC → City Local Time
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            var times = hourly["time"]!.AsArray()
                .Select(t => DateTime.Parse(t!.ToString()))
                .ToList();

            // local interval
            var indices = times
                .Select((t, i) => new { t, i })
                .Where(x => x.t >= nowLocal && x.t <= nowLocal.AddHours(hours))
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

            var tempMin = temps.Min();
            var tempMax = temps.Max();
            var feelsAvg = feels.Average();
            var windAvg = winds.Average();
            var humidityAvg = humidities.Average();

            var rainChance = Math.Round((double)rains.Count(r => r > 0.2) / rains.Count * 100, 0);
            var mainCondition = MapWeatherCode(
                codes.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key
            );

            return new
            {
                location = new { city = cityName, latitude = lat, longitude = lon },
                dateTimeStart = nowLocal.ToString("yyyy-MM-dd HH:mm"),
                intervalHours = hours,
                tempMin,
                tempMax,
                feelsLikeAvg = feelsAvg,
                windAvg = Math.Round(windAvg, 1),
                humidityAvg = Math.Round(humidityAvg, 1),
                rainChance,
                mainCondition
            };
        }

        // -----------------------------
        // ☁️ WEATHER CODE MAPPING
        // -----------------------------
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
