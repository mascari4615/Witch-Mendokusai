using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UIWindow))]
	public class UIInventoryPanel : MonoBehaviour
	{
		private UIItemGrid itemInventoryUI;

		private void Awake()
		{
			itemInventoryUI = GetComponentInChildren<UIItemGrid>(true);
			itemInventoryUI.Init();
		}
	}
}
