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
    private bool _isFileValid = false;

    public CsvFileDataProvider(SimulatorConfig config, ILogger<CsvFileDataProvider> logger, IHostEnvironment env)
    {
        _config = config;
        _logger = logger;
        _env = env;
        InitializeReader();
    }

    private void InitializeReader()
    {
        var fullPath = Path.Combine(_env.ContentRootPath, _config.DataFilePath);
        var fileInfo = new FileInfo(fullPath);

        if (fileInfo.Exists && fileInfo.Length > 0)
        {
            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _reader = new StreamReader(fileStream);
            _isFileValid = true;
            _logger.LogInformation("SUKCES! Plik załadowany: {Path}", fullPath);
        }
        else
        {
            _logger.LogWarning("BŁĄD: Plik danych nie istnieje lub jest pusty. Ścieżka: {Path}", fullPath);
        }
    }

    public async Task<string> GetNextPayloadAsync(CancellationToken cancellationToken)
    {
        if (!_isFileValid || _reader == null) return string.Empty;

        var line = await _reader.ReadLineAsync(cancellationToken);

        if (line == null)
        {
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