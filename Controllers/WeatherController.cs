using Microsoft.AspNetCore.Mvc;
using StyleCast.Backend.Services;

namespace StyleCast.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherService _weatherService;

        public WeatherController(WeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWeather([FromQuery] double lat, [FromQuery] double lon, [FromQuery] int hours = 6)
        {
            try
            {
                var data = await _weatherService.GetWeatherSummary(lat, lon, hours);
                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}