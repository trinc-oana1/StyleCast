using Microsoft.AspNetCore.Authentication.Cookies;
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

// Page to redirect to if not logged in
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/pages/signin.html"; // Redirect here if not authenticated
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    });

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

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();

    // allow access only to signin and signout page
    bool isHtmlPage = path == "/" || (path != null && path.EndsWith(".html"));
    bool isPublicPage = path != null && (path.Contains("/pages/signin.html") || path.Contains("/pages/register.html")); 

    if (isHtmlPage && !isPublicPage)
    {
        // If user is not authenticated, redirect to login
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/pages/signin.html");
            return;
        }
    }

    await next();
});

app.UseDefaultFiles();  // looking for index.html
app.UseStaticFiles();   // using from wwwroot

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// redirecting to start page
app.MapFallbackToFile("pages/signin.html");

app.Run();