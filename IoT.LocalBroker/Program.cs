using System.Text;
using MQTTnet;
using MQTTnet.Server;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Services;

Console.WriteLine("=========================================");
Console.WriteLine("   LOCAL BROKER MQTT (IoT Simulators)  ");
Console.WriteLine("=========================================\n");

var builder = WebApplication.CreateBuilder(args);

// 1. KONFIGURACJA ZALEŻNOŚCI DLA REJESTRU
builder.Services.AddHttpClient();

// Opis brokera, który zostanie wysłany do Panelu Sterowania
builder.Services.AddSingleton(new SimulatorConfig
{
    Protocol = "MQTT Broker",
    TargetAddress = "localhost",
    TopicOrPath = "Port: 1883",
    IsRunning = true,
    ProcessedMessages = 0 // Inicjalizacja licznika
});

builder.Services.AddSingleton<RegistryClient>();

var app = builder.Build();

// POBIERAMY OBIEKT KONFIGURACJI, BY MÓC ZMIENIAĆ JEGO STAN
var brokerConfig = app.Services.GetRequiredService<SimulatorConfig>();

// Wysłanie statusu do Panelu przy starcie aplikacji ORAZ CYKLICZNIE (jak w DataCollector)
app.Lifetime.ApplicationStarted.Register(() =>
{
    var registryClient = app.Services.GetRequiredService<RegistryClient>();
    var timer = new PeriodicTimer(TimeSpan.FromSeconds(0.5)); // Synchronizacja co 2 sekundy

    _ = Task.Run(async () =>
    {
        await registryClient.PushStateAsync(); // Pierwsze wysłanie stanu

        while (await timer.WaitForNextTickAsync())
        {
            await registryClient.PushStateAsync(); // Cykliczne wysyłanie stanu
        }
    });
});

// 2. KONFIGURACJA I INICJALIZACJA BROKERA MQTT
var mqttFactory = new MqttFactory();
var mqttServerOptions = new MqttServerOptionsBuilder()
    .WithDefaultEndpoint()
    .WithDefaultEndpointPort(1883)
    .Build();

var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

// Nasłuchiwanie zdarzeń
mqttServer.ClientConnectedAsync += e =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[+] Podłączono klienta: {e.ClientId}");
    Console.ResetColor();
    return Task.CompletedTask;
};

mqttServer.InterceptingPublishAsync += e =>
{
    var payload = e.ApplicationMessage.PayloadSegment.Count > 0
        ? Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)
        : "Pusty ładunek";

    // INKREMENTACJA LICZNIKA PRZETWORZONYCH WIADOMOŚCI
    brokerConfig.ProcessedMessages++;

    Console.WriteLine($"[MSG] Temat: {e.ApplicationMessage.Topic} | Rozmiar: {e.ApplicationMessage.PayloadSegment.Count} bajtów");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"      Treść: {payload}");
    Console.ResetColor();

    return Task.CompletedTask;
};

// Start brokera, gdy wystartuje główny proces aplikacji
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

// Zatrzymanie brokera przy zamykaniu aplikacji
app.Lifetime.ApplicationStopping.Register(async () =>
{
    Console.WriteLine("Zatrzymywanie brokera...");
    await mqttServer.StopAsync();
});

// 3. URUCHOMIENIE
app.Run("http://localhost:5099");