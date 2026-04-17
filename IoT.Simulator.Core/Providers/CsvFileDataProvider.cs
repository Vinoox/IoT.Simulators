using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IoT.Simulator.Core.Providers;

public class CsvFileDataProvider : IDataProvider, IDisposable
{
    private readonly SimulatorConfig _config;
    private readonly ILogger<CsvFileDataProvider> _logger;
    private readonly IHostEnvironment _env;
    private StreamReader? _reader;
    private bool _isFileValid = true;

    public CsvFileDataProvider(SimulatorConfig config, ILogger<CsvFileDataProvider> logger, IHostEnvironment env)
    {
        _config = config;
        _logger = logger;
        _env = env;
        InitializeReader();
    }

    private void InitializeReader()
    {
        try
        {
            string fullPath = Path.Combine(_env.ContentRootPath, _config.DataFilePath);

            _logger.LogInformation("Szukam pliku pod adresem: {Path}", fullPath);

            if (File.Exists(fullPath))
            {
                _reader = new StreamReader(fullPath);
                _logger.LogInformation("SUKCES! System widzi plik i zaczyna nadawanie.");
                _isFileValid = true;
            }
            else
            {
                _logger.LogWarning("PLIKU NADAL NIE MA! Sprawdź ścieżkę: {Path}", fullPath);
                _isFileValid = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd krytyczny podczas otwierania pliku.");
            _isFileValid = false;
        }
    }

    public async Task<string> GetNextPayloadAsync(CancellationToken cancellationToken)
    {
        if (!_isFileValid || _reader == null)
            return string.Empty;

        var line = await _reader.ReadLineAsync(cancellationToken);

        if (line == null)
        {
            _logger.LogDebug("Osiągnięto koniec pliku. Zapętlam strumień od nowa.");
            _reader.BaseStream.Position = 0;
            _reader.DiscardBufferedData();
            line = await _reader.ReadLineAsync(cancellationToken);
        }

        return line ?? string.Empty;
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}