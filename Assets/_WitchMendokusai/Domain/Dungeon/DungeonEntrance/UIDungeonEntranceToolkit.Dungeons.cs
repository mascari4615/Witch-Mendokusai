using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 던전 목록 받기. NPC 앞에서 열면 그 NPC 의 목록, 마을 시련소처럼 NPC 없이 열면 UINPC.OpenDungeonEntrance 가 먼저 채움
	public partial class UIDungeonEntranceToolkit
	{
		public override void SetNPC(NPCObject npc)
		{
			// 없으면 OpenDungeonEntrance 가 UseDungeonsOf 로 이미 채운 목록을 씀
			if (npc != null)
				UseDungeonsOf(npc.Data);
		}

		public void UseDungeonsOf(NPC source)
		{
			dungeons = NPCUtil.GetDungeons(source);
			// 데모 릴리스 빌드는 개발용 던전 미노출 (사용자 2026-11-12)
			if (Debug.isDebugBuild == false)
				dungeons = dungeons.FindAll(dungeon => dungeon.DevBuildOnly == false);

			if (dungeons == null || dungeons.Count == 0)
				Debug.LogError("No Dungeon Data");

			EnsureButtonCount(dungeons.Count);

			for (int i = 0; i < dungeonSelectButtons.Count; i++)
			{
				if (i < dungeons.Count)
				{
					dungeonRowNames[i].text = GetRowName(dungeons[i].Name);
					dungeonRowIcons[i].style.backgroundImage = new StyleBackground(dungeons[i].Sprite);
					dungeonSelectButtons[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					dungeonSelectButtons[i].style.display = DisplayStyle.None;
				}
			}
		}
	}
}
