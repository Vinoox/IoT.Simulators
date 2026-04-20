using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

builder.Services.AddSingleton(new SimulatorConfig
{
    Protocol = "HTTP Receiver",
    TargetAddress = "localhost:5088",
    TopicOrPath = "/api/collect/{sector}",
    IsRunning = true,
    ProcessedMessages = 0
});

builder.Services.AddSingleton<RegistryClient>();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var registryClient = app.Services.GetRequiredService<RegistryClient>();
    var timer = new PeriodicTimer(TimeSpan.FromSeconds(0.5));

    _ = Task.Run(async () =>
    {
        await registryClient.PushStateAsync();

        while (await timer.WaitForNextTickAsync())
        {
            await registryClient.PushStateAsync();
        }
    });
});

int _totalPackets = 0;

app.MapPost("/api/collect/{sector}", async (string sector, HttpRequest request, SimulatorConfig config) => {

    Interlocked.Increment(ref _totalPackets);
    config.ProcessedMessages = _totalPackets;

    // 1. Odczytanie czystej zawartoœci (³adunku json) prosto ze strumienia HTTP
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();

    // Zabezpieczenie na wypadek pustego ¿¹dania
    if (string.IsNullOrWhiteSpace(payload))
    {
        payload = "Pusty ³adunek";
    }

    // 2. Obliczenie dok³adnego rozmiaru w bajtach
    var byteCount = System.Text.Encoding.UTF8.GetByteCount(payload);

    // 3. Rysowanie logu w konsoli w identycznym formacie jak MQTT
    Console.WriteLine($"[MSG] Œcie¿ka: {request.Path} | Rozmiar: {byteCount} bajtów");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"      Treœæ: {payload}");
    Console.ResetColor();

    return Results.Accepted();
});

app.Run();