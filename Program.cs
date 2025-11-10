using Microsoft.EntityFrameworkCore;
using StyleCast.Backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// ✅ trebuie să adaugi aceste două linii:
app.UseDefaultFiles();  // caută index.html implicit
app.UseStaticFiles();   // servește din wwwroot

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// ✅ fallback dacă nu găsește altceva – redirecționează la pagina ta de start
app.MapFallbackToFile("pages/signin.html");

app.Run();