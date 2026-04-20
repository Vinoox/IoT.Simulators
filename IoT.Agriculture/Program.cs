using System.Text.Json.Serialization;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using IoT.Simulator.Core.Providers;
using IoT.Simulator.Core.Senders;
using IoT.Simulator.Core.Services;
using IoT.Simulator.Core.Workers;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracja globalna formatu JSON
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Pobranie pocz¹tkowych ustawieñ symulatora z pliku appsettings.json
var configState = builder.Configuration
    .GetSection(SimulatorConfig.SectionName)
    .Get<SimulatorConfig>() ?? new SimulatorConfig();

// Rejestracja serwisow
builder.Services.AddSingleton(configState);
builder.Services.AddSingleton<IDataProvider, CsvFileDataProvider>(); // Dostarczyciel danych (z pliku CSV)
builder.Services.AddHttpClient(); // Klient HTTP dla zapytañ REST
builder.Services.AddSingleton<IDataSender, HttpDataSender>(); // Modu³ wysy³ki po HTTP
builder.Services.AddSingleton<IDataSender, MqttDataSender>(); // Modu³ wysy³ki po MQTT
builder.Services.AddHostedService<SimulatorWorker>(); // G³ówna pêtla symulatora pracuj¹ca w tle
builder.Services.AddSingleton<RegistryClient>(); // Klient raportuj¹cy stan do panelu

var app = builder.Build();

// Zdarzenie jednorazowe przy starcie: Zg³oszenie swojej obecnoœci do centralnego Panelu Sterowania
app.Lifetime.ApplicationStarted.Register(() =>
{
    var registryClient = app.Services.GetRequiredService<IoT.Simulator.Core.Services.RegistryClient>();
    _ = Task.Run(() => registryClient.PushStateAsync());
});

// Endpoint GET: Zwraca aktualn¹ konfiguracjê symulatora
app.MapGet("/api/config", (SimulatorConfig currentConfig) => Results.Ok(currentConfig));

// Endpoint PUT: Zdalna aktualizacja ustawieñ serwisu
app.MapPut("/api/config", (
    [FromBody] SimulatorConfig incoming,
    [FromServices] SimulatorConfig singletonConfig,
    [FromServices] RegistryClient registryClient,
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
    singletonConfig.IsRunning = incoming.IsRunning;

    // aktualizacja stanu w panelu
    _ = Task.Run(() => registryClient.PushStateAsync());

    return Results.Ok(singletonConfig);
});

app.Run();