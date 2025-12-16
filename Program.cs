using Microsoft.EntityFrameworkCore;
using StyleCast.Backend.Data;
using StyleCast.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient<WeatherService>();
//aici adaugam si cache service cand o sa fie facut
builder.Services.AddMemoryCache(); // to allow for local memory cache
builder.Services.AddSingleton<ICacheService, CacheService>();

// Database connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Enable CORS for the frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.WithOrigins(
                "http://localhost:5500",
                "http://127.0.0.1:5500",
                "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();  // looking for index.html
app.UseStaticFiles();   // using from wwwroot

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// redirecting to start page
app.MapFallbackToFile("pages/signin.html");

app.Run();