using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public enum DungeonPanelType
	{
		None = -1,
		DungeonRuntime = 0,
		DungeonResult = 1,
	}

	public class UIDungeon : UIPanelGroup<DungeonPanelType>
	{
		[Inject]
		public void Construct(UIManager uiManager) => SetUIManager(uiManager);

		public override bool CanBeClosedByCancelInput => false;
		public override DungeonPanelType DefaultPanel => DungeonPanelType.None;

		// init-order-ok: 이 창들은 이 묶음과 **같은 프리팹 한 덩어리**다(`[Panel] Dungeon` 안에 진행·결과 창).
		// 유니티는 덩어리를 통째로 만든 뒤 깨우기를 돌리므로 찾을 것이 반드시 있고, 꺼진 것까지 포함해 찾는다.
		// 게다가 이 Init 은 모든 깨우기가 끝난 뒤에 불린다(UIPanelGroup.Start). 확인 2026-08-08 / TASK-WM-212.
		public override void Init()
		{
			Panels[DungeonPanelType.DungeonRuntime] = FindAnyObjectByType<UIDungeonRuntime>(FindObjectsInactive.Include);
			Panels[DungeonPanelType.DungeonResult] = FindAnyObjectByType<UIDungeonResult>(FindObjectsInactive.Include);
		}
	}
}