using System.Collections.Generic;

namespace WastelandSurvivor.Game.Systems;

internal static class EncounterLogUtil
{
	public static void AppendAndClamp(List<string> log, string line, int maxLines = 30)
	{
		log.Add(line);
		Clamp(log, maxLines);
	}

	public static void Clamp(List<string> log, int maxLines = 30)
	{
		while (log.Count > maxLines)
			log.RemoveAt(0);
	}
}
