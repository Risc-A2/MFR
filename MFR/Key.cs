namespace MFR;

public class Key
{
	private FastList<int> pressedTracks = new();

	public void AddTrack(int track)
	{
		if (pressedTracks.Contains(track))
			pressedTracks.Remove(track);
		pressedTracks.Add(track);
	}

	public void RemoveTrack(int track)
	{
		pressedTracks.Remove(track);
	}

	public int GetTopTrack()
	{
		int t = 0;
		foreach (var v in pressedTracks)
		{
			t = v;
		}

		return t;
	}
	
	public void Clear()
	{
		pressedTracks.Unlink();
	}
}