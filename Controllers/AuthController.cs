using Microsoft.AspNetCore.Mvc;
using StyleCast.Backend.Data;
using StyleCast.Backend.Models;

namespace StyleCast.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] User user)
        {
            if (_context.Users.Any(u => u.Email == user.Email))
                return BadRequest("User already exists");

            _context.Users.Add(user);
            _context.SaveChanges();
            return Ok("User registered successfully");
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] User user)
        {
            var existing = _context.Users.FirstOrDefault(
                u => u.Email == user.Email && u.Password == user.Password);

            if (existing == null)
                return Unauthorized("Invalid credentials");

            return Ok("Login successful");
        }
    }
}