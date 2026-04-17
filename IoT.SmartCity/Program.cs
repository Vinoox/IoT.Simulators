using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using IoT.Simulator.Core.Providers;
using IoT.Simulator.Core.Senders;
using IoT.Simulator.Core.Workers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfiguracja JSON
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// 2. £adowanie stanu pocz¹tkowego
var configState = builder.Configuration
    .GetSection(SimulatorConfig.SectionName)
    .Get<SimulatorConfig>() ?? new SimulatorConfig();

builder.Services.AddSingleton(configState);

// 3. Rejestracja us³ug rdzeniowych
builder.Services.AddSingleton<IDataProvider, CsvFileDataProvider>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDataSender, HttpDataSender>();
builder.Services.AddSingleton<IDataSender, MqttDataSender>();
builder.Services.AddHostedService<SimulatorWorker>();


var app = builder.Build();

app.MapGet("/api/config", (SimulatorConfig currentConfig) => Results.Ok(currentConfig));

app.MapPut("/api/config", (
    [FromBody] SimulatorConfig incoming,
    [FromServices] SimulatorConfig singletonConfig,
    ILogger<Program> logger) =>
{
    if (incoming.IntervalMilliseconds < 1)
        return Results.BadRequest("Interwa³ nie mo¿e byæ krótszy ni¿ 1ms.");

    if (string.IsNullOrWhiteSpace(incoming.TargetAddress))
        return Results.BadRequest("Adres docelowy nie mo¿e byæ pusty.");

    if (string.IsNullOrWhiteSpace(incoming.TopicOrPath))
        return Results.BadRequest("Œcie¿ka (dla HTTP) lub temat (dla MQTT) nie mo¿e byæ pusta.");

    if (!incoming.Protocol.Equals("HTTP", StringComparison.OrdinalIgnoreCase) &&
        !incoming.Protocol.Equals("MQTT", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("Obs³ugiwane protoko³y to wy³¹cznie HTTP lub MQTT.");

    logger.LogInformation("Zdalna aktualizacja z Panelu: Interval={Interval}, Protocol={Protocol}, Target={Target}",
        incoming.IntervalMilliseconds, incoming.Protocol.ToUpper(), incoming.TargetAddress);

    singletonConfig.Protocol = incoming.Protocol.ToUpper();
    singletonConfig.IntervalMilliseconds = incoming.IntervalMilliseconds;
    singletonConfig.TargetAddress = incoming.TargetAddress;
    singletonConfig.TopicOrPath = incoming.TopicOrPath;

    return Results.Ok(singletonConfig);
});

app.MapGet("/", () => Results.Ok("Symulator IoT pracuje w tle. API dostêpne tylko do u¿ytku wewnêtrznego."));

app.Run();