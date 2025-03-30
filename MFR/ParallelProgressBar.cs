namespace MFR;

public class ParallelProgressBar
{
	private readonly double _total;
	private double _current;
	private readonly object _lock = new object();

	public ParallelProgressBar(double total) => _total = total;

	public void Update(double increment = 1)
	{
		lock (_lock)
		{
			_current += increment;
			double progress = _current / _total;
			Console.Write($"\rProgress: {progress:P}");
		}
	}
}