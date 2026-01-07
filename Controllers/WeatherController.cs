using Microsoft.AspNetCore.Mvc;
using StyleCast.Backend.Services;

namespace StyleCast.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherService _weatherService;
        private readonly ICacheService _cacheService;

        public WeatherController(WeatherService weatherService, ICacheService cacheService)
        {
            _weatherService = weatherService;
            _cacheService = cacheService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWeather([FromQuery] double lat, [FromQuery] double lon, [FromQuery] int hours = 6)
        {
            string cacheKey = $"weather_{lat}_{lon}_{hours}";
            try
            {
                var data = await _weatherService.GetWeatherSummary(lat, lon, hours);
                _cacheService.SetData(cacheKey, data, DateTimeOffset.Now.AddDays(10));
                
                return Ok(data);
            }
            catch (Exception ex)
            {
                var cachedData = _cacheService.GetData<object>(cacheKey);

                if (cachedData != null)
                {
                    return Ok(cachedData);
                }
                
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}