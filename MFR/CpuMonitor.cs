namespace MFR;

using System.Diagnostics;

public class CpuMonitor
{
	private readonly Process _process;
	private TimeSpan _lastCpuTime;
	private DateTime _lastUpdate;
	private double _usage;
    
	public CpuMonitor()
	{
		_process = Process.GetCurrentProcess();
		_lastCpuTime = _process.TotalProcessorTime;
		_lastUpdate = DateTime.UtcNow;
	}
    
	public double GetCurrentCpuUsage()
	{
		var newCpuTime = _process.TotalProcessorTime;
		var newUpdateTime = DateTime.UtcNow;
        
		var cpuUsedMs = (newCpuTime - _lastCpuTime).TotalMilliseconds;
		var timePassedMs = (newUpdateTime - _lastUpdate).TotalMilliseconds;
        
		_lastCpuTime = newCpuTime;
		_lastUpdate = newUpdateTime;
        
		// Calcular porcentaje considerando todos los núcleos
		_usage = (cpuUsedMs / (timePassedMs * Environment.ProcessorCount));
        
		return _usage;
	}
}