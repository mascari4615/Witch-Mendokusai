using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public static class NPCUtil
	{
		public static List<Dungeon> GetDungeons(NPC npc)
		{
			List<int> ids = npc.PanelInfos
					.Where(i => i.Type == NPCPanelType.DungeonEntrance)
					.SelectMany(i => i.DataSOs)
					.Select(i => i.ID)
					.ToList();

			List<Dungeon> dungeons = ids
					.Select(i => SOHelper.Get<Dungeon>(i))
					.ToList();

			if ((dungeons == null) || (dungeons.Count == 0))
			{
				Debug.LogError("No Dungeon Data");
				return new List<Dungeon>();
			}

			return dungeons;
		}
	}
}