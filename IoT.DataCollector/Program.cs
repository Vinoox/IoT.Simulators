using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Services;

// Inicjalizacja aplikacji webowej pe³ni¹cej rolê Kolektora Danych HTTP
var builder = WebApplication.CreateBuilder(args);

// Rejestracja klienta HTTP niezbêdnego do komunikacji z Panelem Sterowania
builder.Services.AddHttpClient();

// Wczytanie stanu pocz¹tkowego kolektora
var initialConfig = builder.Configuration
    .GetSection("SimulatorConfig")
    .Get<SimulatorConfig>() ?? new SimulatorConfig();

// Rejestracja konfiguracji jako Singleton
builder.Services.AddSingleton(initialConfig);

// Rejestracja us³ugi odpowiedzialnej za wysy³anie statystyk do panelu
builder.Services.AddSingleton<RegistryClient>();

var app = builder.Build();

// raportowanie stanu kolektora do panelu
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

// Endpoint nas³uchuj¹cy na dane
app.MapPost("/api/collect/{sector}", async (string sector, HttpRequest request, SimulatorConfig config) => {

    // Bezpieczna dla wielu w¹tków inkrementacja licznika i aktualizacja obiektu konfiguracji
    Interlocked.Increment(ref _totalPackets);
    config.ProcessedMessages = _totalPackets;

    // Pobranie surowej zawartoœci tekstowej (np. JSON) bezpoœrednio ze strumienia wejœciowego
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(payload))
    {
        payload = "Pusty ³adunek";
    }

    var byteCount = System.Text.Encoding.UTF8.GetByteCount(payload);

    Console.WriteLine($"[MSG] Œcie¿ka: {request.Path} | Rozmiar: {byteCount} bajtów");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"      Treœæ: {payload}");
    Console.ResetColor();

    // Potwierdzenie dla symulatora, ¿e pakiet zosta³ pomyœlnie odebrany (HTTP 202)
    return Results.Accepted();
});

app.Run();