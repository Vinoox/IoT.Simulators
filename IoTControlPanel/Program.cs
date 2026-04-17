using IoT.Simulator.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// 1. Konfiguracja JSON, aby Enumy w Swaggerze by³y tekstami (MQTT/HTTP)
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true; // Ignoruj wielkoœæ liter w nazwach pól
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => {
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Przekierowanie na swagger przy starcie
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// MAPA SERWISÓW (Porty symulatorów)
var servicePorts = new Dictionary<TargetService, string>
{
    { TargetService.SmartCity, "http://localhost:5001" },
    { TargetService.SmartGrid, "http://localhost:5002" },
    { TargetService.Agriculture, "http://localhost:5003" },
    { TargetService.Logistics, "http://localhost:5004" },
    { TargetService.Healthcare, "http://localhost:5005" }
};

// ==========================================
// ENDPOINT 1: ZMIANA INTERWA£U
// ==========================================
app.MapPost("/api/control/update-interval", async (TargetService service, int intervalMs, IHttpClientFactory clientFactory) =>
{
    if (!servicePorts.TryGetValue(service, out var targetUrl))
        return Results.NotFound($"Nie znaleziono portu dla {service}");

    Console.WriteLine($"[PANEL] Zmiana interwa³u dla {service} na {intervalMs}ms...");

    return await UpdateRemoteConfig(service, targetUrl, clientFactory, config =>
    {
        // PRZYPISANIE: To tutaj dzieje siê magia
        config.IntervalMilliseconds = intervalMs;
    });
})
.WithTags("Sterowanie")
.WithSummary("Zmienia tylko czêstotliwoœæ (ms)");

// ==========================================
// ENDPOINT 2: ZMIANA PROTOKO£U (I sztywne adresy)
// ==========================================
app.MapPost("/api/control/switch-protocol", async (TargetService service, ProtocolType protocol, IHttpClientFactory clientFactory) =>
{
    if (!servicePorts.TryGetValue(service, out var targetUrl))
        return Results.NotFound();

    string finalAddress = protocol == ProtocolType.MQTT ? "localhost" : "http://localhost:8080";
    string finalTopic = protocol == ProtocolType.MQTT
        ? $"sensors/{service.ToString().ToLower()}"
        : $"/api/collect/{service.ToString().ToLower()}";

    Console.WriteLine($"[PANEL] Prze³¹czanie {service} na {protocol} ({finalAddress})...");

    return await UpdateRemoteConfig(service, targetUrl, clientFactory, config =>
    {
        config.Protocol = protocol.ToString();
        config.TargetAddress = finalAddress;
        config.TopicOrPath = finalTopic;
    });
})
.WithTags("Sterowanie")
.WithSummary("Prze³¹cza protokó³ i ustawia sztywne adresy docelowe");

// ==========================================
// KOLEKTOR DANYCH (Odbiornik na porcie 8080)
// ==========================================
app.MapPost("/api/collect/{sector}", (string sector, object data, ILogger<Program> logger) =>
{
    logger.LogInformation(" >>> [{Sector}] Otrzymano: {Data}", sector.ToUpper(), data);
    return Results.Accepted();
})
.WithTags("Kolektor");

app.Run();

// ==========================================
// LOGIKA POMOCNICZA (Pancerna)
// ==========================================
async Task<IResult> UpdateRemoteConfig(TargetService service, string targetUrl, IHttpClientFactory factory, Action<SimulatorConfig> updateAction)
{
    try
    {
        var client = factory.CreateClient();

        // 1. Pobierz obecn¹ konfiguracjê z Symulatora
        var config = await client.GetFromJsonAsync<SimulatorConfig>($"{targetUrl}/api/config");
        if (config == null) return Results.Problem("Symulator zwróci³ pusty obiekt.");

        // 2. Wykonaj aktualizacjê pól
        updateAction(config);

        // 3. LOG DIAGNOSTYCZNY - SprawdŸ konsolê Panelu!
        Console.WriteLine($"[PANEL] Wysy³am do {service}: Interval={config.IntervalMilliseconds}, Proto={config.Protocol}");

        // 4. Odes³anie poprawionego obiektu
        var response = await client.PostAsJsonAsync($"{targetUrl}/api/config", config);

        if (response.IsSuccessStatusCode)
        {
            return Results.Ok(new { Message = "Zaktualizowano pomyœlnie", CurrentConfig = config });
        }

        return Results.StatusCode((int)response.StatusCode);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[B£¥D PANELU] {ex.Message}");
        return Results.Problem($"Symulator {service} na {targetUrl} nie odpowiada.");
    }
}

// ==========================================
// MODELE
// ==========================================
public enum ProtocolType { MQTT, HTTP }
public enum TargetService { SmartCity, SmartGrid, Agriculture, Logistics, Healthcare }