using System;
using System.Threading;
using System.Threading.Tasks;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
// Usunąłem niepotrzebny using Microsoft.Extensions.Options;

namespace IoT.Simulator.Core.Senders;

public class MqttDataSender : IDataSender, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly SimulatorConfig _config;
    private readonly ILogger<MqttDataSender> _logger;

    // 1. POPRAWKA: Prawidłowe wstrzykiwanie bezpośredniego obiektu (SimulatorConfig config)
    public MqttDataSender(SimulatorConfig config, ILogger<MqttDataSender> logger)
    {
        _config = config;
        _logger = logger;

        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // 2. POPRAWKA: Usunąłem stąd budowanie opcji (_mqttOptions)
    }

    public string Protocol => "MQTT";

    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        // Tworzenie wiadomości jest w bezpiecznym miejscu - zawsze użyje najnowszego _config.TopicOrPath
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_config.TopicOrPath)
            .WithPayload(payload)
            .Build();

        await _mqttClient.PublishAsync(message, cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!_mqttClient.IsConnected)
        {
            // 3. KLUCZOWA ZMIANA: Budujemy opcje tuż przed połączeniem.
            // Dzięki temu sender zawsze użyje adresu, który obecnie widnieje w panelu sterowania.
            var dynamicOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.TargetAddress)
                .Build();

            _logger.LogInformation("Nawiązywanie połączenia z brokerem MQTT: {Address}", _config.TargetAddress);
            await _mqttClient.ConnectAsync(dynamicOptions, cancellationToken);
        }
    }

    public void Dispose()
    {
        // rozłączamy brokera MQTT
        _mqttClient?.Dispose();
    }
}