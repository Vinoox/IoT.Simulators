using System.ComponentModel;
using System.Text.Json.Serialization;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfiguracja serializacji JSON dla Enumów
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => {
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "IoT Control Panel", Version = "v2" });
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IoTControlPanel.Services.ServiceRegistry>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

//app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ==========================================
// ENDPOINT 1: ZMIANA INTERWA£U
// ==========================================
app.MapPost("/api/control/update-interval", async (
    TargetService service,
    int intervalMs,
    IHttpClientFactory clientFactory,
    IConfiguration configuration) =>
{
    string? targetUrl = configuration.GetValue<string>($"ServiceUrls:{service}");
    if (string.IsNullOrEmpty(targetUrl))
        return Results.NotFound($"Nie znaleziono adresu dla {service} w appsettings.json");

    return await UpdateRemoteConfig(targetUrl, clientFactory, config =>
    {
        config.IntervalMilliseconds = intervalMs;
    });
})
.WithTags("Sterowanie")
.WithSummary("Zmienia tylko czêstotliwoœæ nadawania (ms)");

// ==========================================
// ENDPOINT 2A: PRZE£¥CZ NA HTTP (Opcja 2)
// ==========================================
app.MapPost("/api/control/switch-to-http", async (
    TargetService service,
    [DefaultValue("http://localhost:8080")] string targetAddress,
    [DefaultValue("/api/collect/smartcity")] string apiPath,
    IHttpClientFactory clientFactory,
    IConfiguration configuration) =>
{
    string? targetUrl = configuration.GetValue<string>($"ServiceUrls:{service}");
    if (string.IsNullOrEmpty(targetUrl)) return Results.NotFound();

    return await UpdateRemoteConfig(targetUrl, clientFactory, config =>
    {
        config.Protocol = "HTTP";
        config.TargetAddress = targetAddress;
        config.TopicOrPath = apiPath;
    });
})
.WithTags("Sterowanie")
.WithSummary("Konfiguruje wysy³kê danych przez protokó³ HTTP");

// ==========================================
// ENDPOINT 2B: PRZE£¥CZ NA MQTT (Opcja 2)
// ==========================================
app.MapPost("/api/control/switch-to-mqtt", async (
    TargetService service,
    [DefaultValue("localhost")] string brokerAddress,
    [DefaultValue("sensors/")] string topic,
    IHttpClientFactory clientFactory,
    IConfiguration configuration) =>
{
    string? targetUrl = configuration.GetValue<string>($"ServiceUrls:{service}");
    if (string.IsNullOrEmpty(targetUrl)) return Results.NotFound();

    return await UpdateRemoteConfig(targetUrl, clientFactory, config =>
    {
        config.Protocol = "MQTT";
        config.TargetAddress = brokerAddress;
        config.TopicOrPath = topic;
    });
})
.WithTags("Sterowanie")
.WithSummary("Konfiguruje wysy³kê danych przez protokó³ MQTT");

// ==========================================
// KOLEKTOR DANYCH (Odbiornik testowy)
// ==========================================
app.MapPost("/api/collect/{sector}", (string sector, object data, ILogger<Program> logger) =>
{
    logger.LogInformation(" >>> [{Sector}] Otrzymano: {Data}", sector.ToUpper(), data);
    return Results.Accepted();
})
.WithTags("Kolektor");

// ==========================================
// ENDPOINT 3A: WZNOWIENIE NADAWANIA (START)
// ==========================================
app.MapPost("/api/control/start", async (
    TargetService service,
    IHttpClientFactory clientFactory,
    IConfiguration configuration) =>
{
    string? targetUrl = configuration.GetValue<string>($"ServiceUrls:{service}");
    if (string.IsNullOrEmpty(targetUrl))
        return Results.NotFound($"Nie znaleziono adresu dla {service}");

    return await UpdateRemoteConfig(targetUrl, clientFactory, config =>
    {
        config.IsRunning = true;
    });
})
.WithTags("Sterowanie")
.WithSummary("Wznawia nadawanie danych w symulatorze");

// ==========================================
// ENDPOINT 3B: ZATRZYMANIE NADAWANIA (STOP)
// ==========================================
app.MapPost("/api/control/stop", async (
    TargetService service,
    IHttpClientFactory clientFactory,
    IConfiguration configuration) =>
{
    string? targetUrl = configuration.GetValue<string>($"ServiceUrls:{service}");
    if (string.IsNullOrEmpty(targetUrl))
        return Results.NotFound($"Nie znaleziono adresu dla {service}");

    return await UpdateRemoteConfig(targetUrl, clientFactory, config =>
    {
        config.IsRunning = false;
    });
})
.WithTags("Sterowanie")
.WithSummary("Zatrzymuje nadawanie danych w symulatorze");

app.MapPost("/api/registry/push", (
    ServiceRegistrationDto state,
    IoTControlPanel.Services.ServiceRegistry registry) =>
{
    registry.UpdateService(state);
    return Results.Ok();
})
.WithTags("Registry")
.ExcludeFromDescription(); // Opcjonalnie ukryj w Swaggerze

app.MapGet("/api/registry/services", (IoTControlPanel.Services.ServiceRegistry registry) =>
{
    return Results.Ok(registry.GetAllServices());
})
.WithTags("Registry")
.WithSummary("Zwraca pe³ny, aktualny stan wszystkich pod³¹czonych symulatorów");

app.Run();

// ==========================================
// LOGIKA POMOCNICZA (Zarz¹dzanie stanem zdalnym)
// ==========================================
async Task<IResult> UpdateRemoteConfig(string targetUrl, IHttpClientFactory factory, Action<SimulatorConfig> updateAction)
{
    try
    {
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        // 1. Pobranie obecnej konfiguracji
        var config = await client.GetFromJsonAsync<SimulatorConfig>($"{targetUrl}/api/config");
        if (config == null) return Results.Problem("Symulator zwróci³ pusty obiekt.");

        // 2. Modyfikacja obiektu
        updateAction(config);

        // 3. Wys³anie zaktualizowanego stanu (PUT)
        var response = await client.PutAsJsonAsync($"{targetUrl}/api/config", config);

        if (response.IsSuccessStatusCode)
        {
            return Results.Ok(new
            {
                Status = "Success",
                AppliedConfig = config
            });
        }

        var error = await response.Content.ReadAsStringAsync();
        return Results.Problem(detail: error, statusCode: (int)response.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 503);
    }
}

public enum TargetService { SmartCity, SmartGrid, Agriculture, Logistics, Healthcare }