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
		[SerializeField] private GameObject tabBackground = null;

		public override TabPanelType DefaultPanel => TabPanelType.None;

		public override void Init()
		{
			Panels[TabPanelType.TabMenu] = FindFirstObjectByType<UITabMenu>(FindObjectsInactive.Include);

			Panels[TabPanelType.MagicBook] = FindFirstObjectByType<UIMagicBookPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Quest] = FindFirstObjectByType<UIQuestPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Inventory] = FindFirstObjectByType<UITabInventoryPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Doll] = FindFirstObjectByType<UIDollPanel>(FindObjectsInactive.Include);
			Panels[TabPanelType.Setting] = FindFirstObjectByType<UISetting>(FindObjectsInactive.Include);
			Panels[TabPanelType.Map] = FindFirstObjectByType<UIMap>(FindObjectsInactive.Include);

			OnPanelChanged += (_) =>
			{
				bool isTabOpen = IsPanelOpen;
				tabBackground.SetActive(isTabOpen);
			};
		}
	}
}