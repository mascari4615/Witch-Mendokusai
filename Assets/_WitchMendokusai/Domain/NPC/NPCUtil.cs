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

		/// <summary>
		/// 티메토 허브에 띄울 미니게임 목록 (TASK-WM-195).
		/// GetDungeons 와 달리 **ID → SOHelper.Get 왕복을 안 한다** — PanelInfos.DataSOs 가 이미 직접 참조를
		/// 들고 있고, ID 왕복은 런타임 DataSO 레지스트리(AssetPrefixes+Addressable 등록) 에 의존해
		/// 등록이 어긋나면 *조용히 빈 목록*이 되는 알려진 함정을 탄다. 직접 캐스팅이 그 의존을 제거.
		/// </summary>
		public static List<MinigameEntrySO> GetMinigameEntries(NPC npc)
		{
			List<MinigameEntrySO> entries = npc.PanelInfos
					.Where(i => i.Type == NPCPanelType.Hub)
					.SelectMany(i => i.DataSOs)
					.OfType<MinigameEntrySO>()
					.ToList();

			if (entries.Count == 0)
				Debug.LogWarning("[Hub] 미니게임 엔트리 0 — 티메토 NPC 의 PanelInfos(Hub) 에 MinigameEntrySO 가 들어있는지 확인.");

			return entries;
		}
	}
}
