using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Server;

namespace IoT.LocalBroker;

static class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=========================================");
        Console.WriteLine("   LOCAL BROKER MQTT (IoT Simulators)  ");
        Console.WriteLine("=========================================\n");

        // Inicjalizacja fabryki MQTT
        var mqttFactory = new MqttFactory();

        var mqttServerOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(1883)
            .Build();

        // Utworzenie instancji serwera
        using var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

        // Interceptor zdarzeń podłączenia klienta
        mqttServer.ClientConnectedAsync += e =>
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[+] Podłączono klienta: {e.ClientId}");
            Console.ResetColor();
            return Task.CompletedTask;
        };

        // Logowanie całego ruchu
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

        // Uruchomienie serwera
        try
        {
            await mqttServer.StartAsync();
            Console.WriteLine("Broker został uruchomiony pomyślnie. Nasłuchuje na porcie 1883...");
            Console.WriteLine("Naciśnij [ENTER], aby wyłączyć brokera.\n");

            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Nie udało się uruchomić brokera. Błąd: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            await mqttServer.StopAsync();
        }
    }
}