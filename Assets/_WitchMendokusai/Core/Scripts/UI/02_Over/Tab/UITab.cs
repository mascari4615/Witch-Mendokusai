using UnityEngine;

namespace WitchMendokusai
{
	public enum TabPanelType
	{
		None = -1,

		MagicBook = 0,
		Quest = 1,
		Doll = 2,
		Map = 3,

		Count = 4,

		TabMenu = 100,
	}

	public class UITab : UIPanelGroup<TabPanelType>
	{
		[Header("Prefabs")]
		[SerializeField] private UITabMenu tabMenuPrefab = null;
		[SerializeField] private UIMagicBookPanel magicBookPanelPrefab = null;
		[SerializeField] private UIQuestPanel questPanelPrefab = null;
		[SerializeField] private UIDollPanel dollPanelPrefab = null;
		[SerializeField] private UIMap mapPanelPrefab = null;

		[Header("References")]
		[SerializeField] private GameObject tabBackground = null;

		public override bool CanBeClosedByCancelInput => true;
		public override TabPanelType DefaultPanel => TabPanelType.None;

		public override void Init()
		{
			Panels[TabPanelType.TabMenu] = Instantiate(tabMenuPrefab, transform);

			Panels[TabPanelType.MagicBook] = Instantiate(magicBookPanelPrefab, transform);
			Panels[TabPanelType.Quest] = Instantiate(questPanelPrefab, transform);
			Panels[TabPanelType.Doll] = Instantiate(dollPanelPrefab, transform);
			Panels[TabPanelType.Map] = Instantiate(mapPanelPrefab, transform);

			OnPanelChanged += (_) =>
			{
				bool isTabOpen = IsPanelOpen;
				tabBackground.SetActive(isTabOpen);
				CameraManager.Instance.SetUICameraMode(UICameraMode.Tab, isTabOpen);
			};
		}
	}
}