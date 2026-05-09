using UnityEngine;

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
		public override bool CanBeClosedByCancelInput => false;
		public override DungeonPanelType DefaultPanel => DungeonPanelType.None;

		public override void Init()
		{
			Panels[DungeonPanelType.DungeonRuntime] = FindAnyObjectByType<UIDungeonRuntime>(FindObjectsInactive.Include);
			Panels[DungeonPanelType.DungeonResult] = FindAnyObjectByType<UIDungeonResult>(FindObjectsInactive.Include);
		}
	}
}