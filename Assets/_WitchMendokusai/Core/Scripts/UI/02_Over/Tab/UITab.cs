using UnityEngine;

namespace WitchMendokusai
{
	public enum TabPanelType
	{
		None = -1,

		MagicBook = 0,
		Map = 1,

		Count = 2,

		TabMenu = 100,
	}

	public class UITab : UIPanelGroup<TabPanelType>
	{
		[Header("Prefabs")]
		[SerializeField] private UITabMenu tabMenuPrefab = null;
		[SerializeField] private UIMagicBookPanel magicBookPanelPrefab = null;
		[SerializeField] private UIMap mapPanelPrefab = null;

		[Header("References")]
		[SerializeField] private GameObject tabBackground = null;

		public override bool CanBeClosedByCancelInput => true;
		public override TabPanelType DefaultPanel => TabPanelType.None;

		public override void Init()
		{
			Panels[TabPanelType.TabMenu] = Instantiate(tabMenuPrefab, transform);

			Panels[TabPanelType.MagicBook] = Instantiate(magicBookPanelPrefab, transform);
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