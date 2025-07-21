// Fix 1: Update Program.cs to handle enum serialization
using Poker.Play;
using System.Text.Json.Serialization;

var commandLineArgs = Environment.GetCommandLineArgs();

var builder = WebApplication.CreateBuilder(args);

// Add services for both console and API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Fix JSON enum serialization - allow string enum conversion
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Keep original property names
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for Flask integration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFlask", policy =>
    {
        policy.WithOrigins("http://localhost:5000", "http://127.0.0.1:5000", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add logging for better debugging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure API pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use CORS before routing
app.UseCors("AllowFlask");

app.UseRouting();
app.MapControllers();

// Check if running as API or console
if (commandLineArgs.Contains("--api"))
{
    Console.WriteLine("🚀 Starting Poker API Server...");
    Console.WriteLine("📖 API Documentation: http://localhost:5001/swagger");
    Console.WriteLine("🌐 CORS enabled for Flask integration");
    Console.WriteLine("🔑 API Key required: poker-game-api-key-2024");
    app.Run("http://localhost:5001");
}
else
{
    Console.WriteLine("🎮 Starting Console Poker Game...");
    var gameManager = GameStarter.StartGame();
    
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadLine();
}