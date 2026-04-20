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

        // POPRAWKA: Użycie wbudowanego, bardzo precyzyjnego timera
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_currentConfig.IntervalMilliseconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var currentInterval = TimeSpan.FromMilliseconds(_currentConfig.IntervalMilliseconds);
                if (timer.Period != currentInterval)
                {
                    timer.Period = currentInterval;
                }

                if (_currentConfig.IsRunning)
                {
                    var payload = await _dataProvider.GetNextPayloadAsync(stoppingToken);

                    if (string.IsNullOrWhiteSpace(payload))
                        continue;

                    var activeSender = _senders.FirstOrDefault(s =>
                        s.Protocol.Equals(_currentConfig.Protocol, StringComparison.OrdinalIgnoreCase));

                    if (activeSender != null)
                    {
                        await activeSender.SendAsync(payload, stoppingToken);
                        _logger.LogInformation("[{Protocol}] Wysłano pakiet na: {Topic}", activeSender.Protocol, _currentConfig.TopicOrPath);
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
        }
    }
}