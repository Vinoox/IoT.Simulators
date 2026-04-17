using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IoT.Simulator.Core.Providers;

public class CsvFileDataProvider : IDataProvider, IDisposable
{
    private readonly SimulatorConfig _config;
    private readonly ILogger<CsvFileDataProvider> _logger;
    private StreamReader? _reader;
    private bool _isFileValid = true;

    // 3. Wstrzykujemy bezpośrednio nasz Singleton!
    public CsvFileDataProvider(SimulatorConfig config, ILogger<CsvFileDataProvider> logger)
    {
        _config = config;
        _logger = logger;
        InitializeReader();
    }

    private void InitializeReader()
    {
        try
        {
            // 1. Pobieramy Content Root Path (folder projektu)
            string projectRoot = AppDomain.CurrentDomain.BaseDirectory;

            // W środowisku deweloperskim (VS) musimy wyjść z bin/Debug/net8.0
            // Directory.GetCurrentDirectory() często wskazuje na folder projektu, 
            // ale najpewniej jest użyć tego:
            string workingDir = Directory.GetCurrentDirectory();

            // 2. Łączymy: C:\...\IoT.SmartGrid + data/energy_data.txt
            string fullPath = Path.Combine(workingDir, _config.DataFilePath);

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
        {
            return string.Empty;
        }

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