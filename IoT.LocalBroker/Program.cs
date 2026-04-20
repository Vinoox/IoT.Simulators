using System.Text;
using MQTTnet;
using MQTTnet.Server;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Services;

Console.WriteLine("=========================================");
Console.WriteLine("   LOCAL BROKER MQTT (IoT Simulators)  ");
Console.WriteLine("=========================================\n");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();


// Wczytanie stanu początkowego brokera
var initialConfig = builder.Configuration
    .GetSection("SimulatorConfig")
    .Get<SimulatorConfig>() ?? new SimulatorConfig();

builder.Services.AddSingleton(initialConfig);

// Rejestracja usługi wysyłającej stan serwera do Panelu Sterowania
builder.Services.AddSingleton<RegistryClient>();

var app = builder.Build();

// Pobranie obiektu konfiguracji z pamięci
var brokerConfig = app.Services.GetRequiredService<SimulatorConfig>();

// Uruchomienie asynchronicznej pętli cyklicznie raportującej stan do Panelu Sterowania
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

// Konfiguracja serwera MQTT 
var mqttFactory = new MqttFactory();
var mqttServerOptions = new MqttServerOptionsBuilder()
    .WithDefaultEndpoint()
    .WithDefaultEndpointPort(1883)
    .Build();

var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

// Obsługa zdarzenia podłączenia nowego urządzenia (symulatora) do brokera
mqttServer.ClientConnectedAsync += e =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[+] Podłączono klienta: {e.ClientId}");
    Console.ResetColor();
    return Task.CompletedTask;
};

// Przechwytywanie przychodzących wiadomości
mqttServer.InterceptingPublishAsync += e =>
{
    var payload = e.ApplicationMessage.PayloadSegment.Count > 0
        ? Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)
        : "Pusty ładunek";

    brokerConfig.ProcessedMessages++;

    Console.WriteLine($"[MSG] Temat: {e.ApplicationMessage.Topic} | Rozmiar: {e.ApplicationMessage.PayloadSegment.Count} bajtów");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"      Treść: {payload}");
    Console.ResetColor();

    return Task.CompletedTask;
};

// Start serwera MQTT 
app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        await mqttServer.StartAsync();
        Console.WriteLine("Broker został uruchomiony pomyślnie. Nasłuchuje na porcie 1883...");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Nie udało się uruchomić brokera. Błąd: {ex.Message}");
        Console.ResetColor();
    }
});

// Zatrzymanie serwera MQTT 
app.Lifetime.ApplicationStopping.Register(async () =>
{
    Console.WriteLine("Zatrzymywanie brokera...");
    await mqttServer.StopAsync();
});

app.Run("http://localhost:5099");