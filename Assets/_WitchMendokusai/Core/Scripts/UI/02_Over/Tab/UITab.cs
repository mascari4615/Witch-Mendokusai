using UnityEngine;

namespace WitchMendokusai
{
	public enum TabPanelType
	{
		None = -1,

		MagicBook = 0,
		Quest = 1,
		Inventory = 2,
		Doll = 3,
		Setting = 4,
		Map = 5,

		Count = 6,

		TabMenu = 100,
	}

	public class UITab : UIContentBase<TabPanelType>
	{
		[Header("Prefabs")]
		[SerializeField] private UITabMenu tabMenuPrefab = null;
		[SerializeField] private UIMagicBookPanel magicBookPanelPrefab = null;
		[SerializeField] private UIQuestPanel questPanelPrefab = null;
		[SerializeField] private UITabInventoryPanel inventoryPanelPrefab = null;
		[SerializeField] private UIDollPanel dollPanelPrefab = null;
		[SerializeField] private UISetting settingPanelPrefab = null;
		[SerializeField] private UIMap mapPanelPrefab = null;

		[Header("References")]
		[SerializeField] private GameObject tabBackground = null;

		public override TabPanelType DefaultPanel => TabPanelType.None;

		public override void Init()
		{
			Panels[TabPanelType.TabMenu] = Instantiate(tabMenuPrefab, transform);

			Panels[TabPanelType.MagicBook] = Instantiate(magicBookPanelPrefab, transform);
			Panels[TabPanelType.Quest] = Instantiate(questPanelPrefab, transform);
			Panels[TabPanelType.Inventory] = Instantiate(inventoryPanelPrefab, transform);
			Panels[TabPanelType.Doll] = Instantiate(dollPanelPrefab, transform);
			Panels[TabPanelType.Setting] = Instantiate(settingPanelPrefab, transform);
			Panels[TabPanelType.Map] = Instantiate(mapPanelPrefab, transform);

			OnPanelChanged += (_) =>
			{
				bool isTabOpen = IsPanelOpen;
				tabBackground.SetActive(isTabOpen);
			};
		}
	}
}