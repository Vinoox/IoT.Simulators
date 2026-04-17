using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using IoT.Simulator.Core.Providers;
using IoT.Simulator.Core.Senders;
using IoT.Simulator.Core.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; // Potrzebne do [FromBody] i [FromServices]
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfiguracja JSON
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// 2. £adowanie i rejestracja Singletona
var configState = builder.Configuration
    .GetSection(SimulatorConfig.SectionName)
    .Get<SimulatorConfig>() ?? new SimulatorConfig();

builder.Services.AddSingleton(configState);

// 3. Rejestracja us³ug
builder.Services.AddSingleton<IDataProvider, CsvFileDataProvider>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDataSender, HttpDataSender>();
builder.Services.AddSingleton<IDataSender, MqttDataSender>();
builder.Services.AddHostedService<SimulatorWorker>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();



// ==========================================
// INTERFEJS ZARZ¥DCZY (Pancerny Endpoint)
// ==========================================

app.MapGet("/api/config", (SimulatorConfig currentConfig) => Results.Ok(currentConfig));

app.MapPost("/api/config", (
    [FromBody] SimulatorConfig incoming,           // To bierze dane z Panelu Sterowania (JSON)
    [FromServices] SimulatorConfig singletonConfig, // To bierze ten sam obiekt, który ma Worker
    ILogger<Program> logger) =>
{
    // LOGI PRZED ZMIAN¥
    logger.LogInformation(">>> ODEBRANO JSON: Interval={Interval}", incoming.IntervalMilliseconds);
    logger.LogInformation(">>> STARY STAN SINGLETONA: Interval={Interval}", singletonConfig.IntervalMilliseconds);

    // Rêczne przepisywanie - JEDYNA PEWNA METODA
    singletonConfig.Protocol = incoming.Protocol;
    singletonConfig.IntervalMilliseconds = incoming.IntervalMilliseconds;
    singletonConfig.TargetAddress = incoming.TargetAddress;
    singletonConfig.TopicOrPath = incoming.TopicOrPath;

    // LOG PO ZMIANIE
    logger.LogInformation(">>> NOWY STAN SINGLETONA: Interval={Interval}", singletonConfig.IntervalMilliseconds);

    return Results.Ok(singletonConfig);
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();