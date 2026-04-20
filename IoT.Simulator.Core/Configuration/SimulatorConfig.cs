namespace IoT.Simulator.Core.Configuration;

public class SimulatorConfig
{
    public const string SectionName = "SimulatorConfig";

    public string Protocol { get; set; } = "HTTP";
    public string TargetAddress { get; set; } = "http://localhost:8080";
    public string TopicOrPath { get; set; } = string.Empty;
    public int IntervalMilliseconds { get; set; } = 9999;
    public string DataFilePath { get; set; } = string.Empty;
    public bool IsRunning { get; set; } = true;
}