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
    IsRunning = true
});

builder.Services.AddSingleton<RegistryClient>();

var app = builder.Build();

// Wysłanie statusu do Panelu przy starcie aplikacji
app.Lifetime.ApplicationStarted.Register(() =>
{
    var registryClient = app.Services.GetRequiredService<RegistryClient>();
    _ = Task.Run(() => registryClient.PushStateAsync());
});

// 2. KONFIGURACJA I INICJALIZACJA BROKERA MQTT
var mqttFactory = new MqttFactory();
var mqttServerOptions = new MqttServerOptionsBuilder()
    .WithDefaultEndpoint()
    .WithDefaultEndpointPort(1883)
    .Build();

var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

// Nasłuchiwanie zdarzeń (Twoja dotychczasowa logika logowania)
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

// Zatrzymanie brokera przy zamykaniu aplikacji (np. po wciśnięciu Ctrl+C)
app.Lifetime.ApplicationStopping.Register(async () =>
{
    Console.WriteLine("Zatrzymywanie brokera...");
    await mqttServer.StopAsync();
});

// 3. URUCHOMIENIE (Blokuje proces, zastępuje wysłużone Console.ReadLine)
// Podajemy fikcyjny port (np. 5099) dla pustej powłoki webowej, aby nie kolidował z niczym innym
app.Run("http://localhost:5099");