using System;
using System.Threading;
using System.Threading.Tasks;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace IoT.Simulator.Core.Senders;

public class MqttDataSender : IDataSender, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly SimulatorConfig _config;
    private readonly ILogger<MqttDataSender> _logger;
    private string _lastConnectedAddress = string.Empty;

    public MqttDataSender(SimulatorConfig config, ILogger<MqttDataSender> logger)
    {
        _config = config;
        _logger = logger;
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
    }

    public string Protocol => "MQTT";

    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_config.TopicOrPath)
            .WithPayload(payload)
            .Build();

        await _mqttClient.PublishAsync(message, cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient.IsConnected && !string.Equals(_lastConnectedAddress, _config.TargetAddress, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Wykryto zmianę adresu. Rozłączanie z {Old} i łączenie z {New}", _lastConnectedAddress, _config.TargetAddress);
            await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);
        }

        if (!_mqttClient.IsConnected)
        {
            var dynamicOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.TargetAddress)
                .Build();

            _logger.LogInformation("Nawiązywanie połączenia z brokerem MQTT: {Address}", _config.TargetAddress);
            await _mqttClient.ConnectAsync(dynamicOptions, cancellationToken);

            _lastConnectedAddress = _config.TargetAddress;
        }
    }

    public void Dispose()
    {
        _mqttClient?.Dispose();
    }
}