using Microsoft.AspNetCore.Mvc;
using StyleCast.Backend.Services;

namespace StyleCast.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherService _weatherService;
        private readonly CacheService _cacheService;

        public WeatherController(WeatherService weatherService, CacheService cacheService)
        {
            _weatherService = weatherService;
            _cacheService = cacheService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWeather([FromQuery] double lat, [FromQuery] double lon, [FromQuery] int hours = 6)
        {
            try
            {
                var data = await _weatherService.GetWeatherSummary(lat, lon, hours);
                
                string cacheKey = $"weather_{hours}";
                _cacheService.SetData(cacheKey, data, DateTimeOffset.Now.AddDays(10));
                
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}