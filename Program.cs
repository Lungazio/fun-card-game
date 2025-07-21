using Poker.Play;

// Get command line arguments
var commandLineArgs = Environment.GetCommandLineArgs();

var builder = WebApplication.CreateBuilder(args);

// Add services for both console and API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure API pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

// Check if running as API or console
if (commandLineArgs.Contains("--api"))
{
    // Run as API server
    Console.WriteLine("🚀 Starting Poker API Server...");
    Console.WriteLine("📖 API Documentation: http://localhost:5001/swagger");
    app.Run("http://localhost:5001");
}
else
{
    // Run original console game
    Console.WriteLine("🎮 Starting Console Poker Game...");
    var gameManager = GameStarter.StartGame();
    
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadLine();
}