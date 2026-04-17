using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using IoT.Simulator.Core.Providers;
using IoT.Simulator.Core.Senders;
using IoT.Simulator.Core.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// 1. Zmiana na WebApplicationBuilder, aby obs³u¿yæ ¿¹dania HTTP (REST API)
var builder = WebApplication.CreateBuilder(args);

// 2. £adowanie konfiguracji z pliku
var configState = builder.Configuration
    .GetSection(SimulatorConfig.SectionName)
    .Get<SimulatorConfig>() ?? new SimulatorConfig();

// Dodajemy nasz obiekt konfiguracji do kontenera. 
// Worker i nasze API bêd¹ wspó³dzieliæ dok³adnie ten SAM obiekt w pamiêci.
builder.Services.AddSingleton(configState);

// 3. Rejestracja Providera (Odczyt CSV)
builder.Services.AddSingleton<IDataProvider, CsvFileDataProvider>();

// 4. Rejestracja WSZYSTKICH strategii wysy³ki naraz
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDataSender, HttpDataSender>();
builder.Services.AddSingleton<IDataSender, MqttDataSender>();

// 5. Rejestracja naszego silnika pracuj¹cego w tle
builder.Services.AddHostedService<SimulatorWorker>();

var app = builder.Build();

// ==========================================
// INTERFEJS ZARZ¥DCZY (REST API)
// ==========================================

// Endpoint 1: Pobieranie obecnej konfiguracji
app.MapGet("/api/config", (SimulatorConfig currentConfig) =>
{
    return Results.Ok(currentConfig);
});

// Endpoint 2: Aktualizacja konfiguracji w locie
app.MapPost("/api/config", (SimulatorConfig newConfig, SimulatorConfig currentConfig) =>
{
    // Aktualizujemy w³aœciwoœci naszego Singletona w pamiêci RAM
    currentConfig.Protocol = newConfig.Protocol;
    currentConfig.IntervalMilliseconds = newConfig.IntervalMilliseconds;
    currentConfig.TargetAddress = newConfig.TargetAddress;
    currentConfig.TopicOrPath = newConfig.TopicOrPath;

    return Results.Ok(new
    {
        Message = "Konfiguracja zmieniona na ¿ywo!",
        CurrentState = currentConfig
    });
});

app.Run();