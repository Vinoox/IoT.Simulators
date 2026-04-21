using System.ComponentModel;
using System.Text.Json.Serialization;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Models;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => {
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IoTControlPanel.Services.ServiceRegistry>();

builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<RegistryHub>("/registryHub");

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
});

// ==========================================
// ENDPOINT 2A: PRZE£¥CZ NA HTTP
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
});

// ==========================================
// ENDPOINT 2B: PRZE£¥CZ NA MQTT
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
});

// ==========================================
// ENDPOINT 3A: WZNOWIENIE NADAWANIA
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
});

// ==========================================
// ENDPOINT 3B: ZATRZYMANIE NADAWANIA
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
});

app.MapPost("/api/registry/push", async (
    ServiceRegistrationDto state,
    IoTControlPanel.Services.ServiceRegistry registry,
    IHubContext<RegistryHub> hubContext) =>
{
    registry.UpdateService(state);

    await hubContext.Clients.All.SendAsync("ReceiveRegistryUpdate", registry.GetAllServices());

    return Results.Ok();
});

app.MapGet("/api/registry/services", (IoTControlPanel.Services.ServiceRegistry registry) =>
{
    return Results.Ok(registry.GetAllServices());
});


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

public class RegistryHub : Hub
{
}