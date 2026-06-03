using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// NPC Unit이 사용 중
	public enum NPCPanelType
	{
		None = -1,

		NPC = 0,
		Shop = 1,
		DungeonEntrance = 2,
		Pot = 3,
		Anvil = 4,
		Furnace = 5,
		CraftingTable = 6,
		Upgrade = 7,
		Lab = 8,
		CauldronMap = 9, // TASK-WM-174 5b-5 — 솥 속의 지도(공존, 기존 Pot 과 별개)

		Count = 10,
	}

	public class UINPC : UIPanelGroup<NPCPanelType>
	{
		[Inject]
		public void Construct(UIManager uiManager) => SetUIManager(uiManager);

		public override bool CanBeClosedByCancelInput => true;
		public override NPCPanelType DefaultPanel => NPCPanelType.None;

		public override void Init()
		{
			Panels[NPCPanelType.NPC] = FindAnyObjectByType<UINPCMenu>(FindObjectsInactive.Include);

			Panels[NPCPanelType.Shop] = FindAnyObjectByType<UIShop>(FindObjectsInactive.Include);
			Panels[NPCPanelType.DungeonEntrance] = UIManager.CreateToolkitPanel<UIDungeonEntranceToolkit>(); // WM-113 S3-F: uGUI UIDungeonEntrance → Toolkit (S3-A/C/D/E 체인 first-use, line 38 Pot S2 패턴 동형). 구 UIDungeonEntrance orphan → E deletion
			Panels[NPCPanelType.Pot] = UIManager.CreateToolkitPanel<UIPotToolkit>(); // WM-113 S2: 구 빈 uGUI UIPot 스텁 → Toolkit (잠복크래시 해소·substrate first-use)
			Panels[NPCPanelType.CauldronMap] = UIManager.CreateToolkitPanel<UICauldronMapPanel>(); // TASK-WM-174 5b-5: 솥 속의 지도(공존)
			Panels[NPCPanelType.Anvil] = FindAnyObjectByType<UIAnvil>(FindObjectsInactive.Include);
			Panels[NPCPanelType.Furnace] = FindAnyObjectByType<UIFurnace>(FindObjectsInactive.Include);
			Panels[NPCPanelType.CraftingTable] = FindAnyObjectByType<UICraftingTable>(FindObjectsInactive.Include);
			Panels[NPCPanelType.Upgrade] = FindAnyObjectByType<UIUpgrade>(FindObjectsInactive.Include);
			Panels[NPCPanelType.Lab] = FindAnyObjectByType<UILab>(FindObjectsInactive.Include);
		}
	}
}