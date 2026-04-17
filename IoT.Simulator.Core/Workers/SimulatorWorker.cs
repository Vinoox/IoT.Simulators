using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoT.Simulator.Core.Workers;

public class SimulatorWorker : BackgroundService
{
    private readonly ILogger<SimulatorWorker> _logger;
    private readonly IDataProvider _dataProvider;
    private readonly IEnumerable<IDataSender> _senders;
    private readonly SimulatorConfig _currentConfig;

    public SimulatorWorker(
        ILogger<SimulatorWorker> logger,
        IDataProvider dataProvider,
        IEnumerable<IDataSender> senders,
        SimulatorConfig currentConfig)
    {
        _logger = logger;
        _dataProvider = dataProvider;
        _senders = senders;
        _currentConfig = currentConfig;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Silnik IoT wystartował. Początkowy interwał: {Interval}ms", _currentConfig.IntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var payload = await _dataProvider.GetNextPayloadAsync(stoppingToken);

                // Zabezpieczenie przed pustymi ramkami po zmianie struktury danych
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    var activeSender = _senders.FirstOrDefault(s =>
                        s.Protocol.Equals(_currentConfig.Protocol, StringComparison.OrdinalIgnoreCase));

                    if (activeSender != null)
                    {
                        await activeSender.SendAsync(payload, stoppingToken);
                        _logger.LogInformation("[{Protocol}] Wysłano pakiet danych na temat/ścieżkę: {Topic}", activeSender.Protocol, _currentConfig.TopicOrPath);
                    }
                    else
                    {
                        _logger.LogWarning("Brak strategii wysyłki dla protokołu: {Protocol}", _currentConfig.Protocol);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas cyklu pracy symulatora.");
            }

            int waitedMilliseconds = 0;
            int stepMilliseconds = 100;

            while (waitedMilliseconds < _currentConfig.IntervalMilliseconds && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(stepMilliseconds, stoppingToken);
                waitedMilliseconds += stepMilliseconds;
            }
        }
    }
}