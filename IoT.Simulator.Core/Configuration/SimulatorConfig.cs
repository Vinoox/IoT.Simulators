namespace IoT.Simulator.Core.Configuration;

public class SimulatorConfig
{
    public const string SectionName = "SimulatorConfig";

    public string Protocol { get; set; } = "HTTP"; 
    public string TargetAddress { get; set; } = string.Empty; 
    public string TopicOrPath { get; set; } = string.Empty; 
    public int IntervalMilliseconds { get; set; } = 5000;

    public string DataSourceType { get; set; } = "File";
    
    public string DataFilePath { get; set; } = string.Empty; 
}