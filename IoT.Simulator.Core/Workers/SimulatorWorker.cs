using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
                // Pobranie danych
                var payload = await _dataProvider.GetNextPayloadAsync(stoppingToken);

                if (!string.IsNullOrEmpty(payload))
                {
                    // Pobranie aktualnego protokołu
                    var activeSender = _senders.FirstOrDefault(s =>
                        s.Protocol.Equals(_currentConfig.Protocol, StringComparison.OrdinalIgnoreCase));

                    if (activeSender != null)
                    {
                        // Wysłanie danych przy użyciu wybranego protokołu
                        await activeSender.SendAsync(payload, stoppingToken);
                        _logger.LogInformation("[{Protocol}] Wysłano pakiet danych.", activeSender.Protocol);
                    }
                    else
                    {
                        _logger.LogWarning("Brak strategii wysyłki dla protokołu: {Protocol}", _currentConfig.Protocol);
                    }
                }
                else
                {
                    _logger.LogWarning("Otrzymano pusty pakiet z dostawcy danych.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas cyklu pracy symulatora.");
            }

            // Pobranie aktualnego interwału z konfiguracji przed kolejnym cyklem
            await Task.Delay(_currentConfig.IntervalMilliseconds, stoppingToken);
        }
    }
}