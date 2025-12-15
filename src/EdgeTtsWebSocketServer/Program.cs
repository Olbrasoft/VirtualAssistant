using Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Register HttpClient for EdgeTtsService to communicate with ContinuousListener
builder.Services.AddHttpClient<EdgeTtsService>();

// Configure Kestrel to listen on specific port
builder.WebHost.ConfigureKestrel(options =>
{
    var port = builder.Configuration.GetValue<int>("EdgeTts:Port", 5555);
    options.ListenAnyIP(port);
});

var app = builder.Build();

app.MapControllers();

// Simple health check endpoint
app.MapGet("/", () => new
{
    service = "Edge TTS WebSocket Server",
    status = "running",
    version = "1.1.0",
    endpoints = new[]
    {
        "POST /api/speech/speak - Convert text to speech",
        "POST /api/speech/stop - Stop current playback",
        "DELETE /api/speech/cache - Clear cache"
    }
});

app.Run();
