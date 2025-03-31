namespace MFR_Core;

public class Key
{
	private FastList<uint> pressedTracks = new();

	public void AddTrack(uint track)
	{
		if (pressedTracks.Contains(track))
			pressedTracks.Remove(track);
		pressedTracks.Add(track);
	}

	public void RemoveTrack(uint track)
	{
		pressedTracks.Remove(track);
	}

	public uint GetTopTrack()
	{
		uint t = 0;
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