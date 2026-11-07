using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(ShopUISettings), menuName = "WM/UI/ShopUISettings")]
	public class ShopUISettings : ScriptableObject
	{
		[field: SerializeField] public StyleSheet StyleSheet { get; private set; }
		[field: SerializeField] public string PanelName { get; private set; } = "Shop";
		[field: SerializeField] public Sprite PanelIcon { get; private set; }
		[field: SerializeField] public int GridColumns { get; private set; } = 7;
		[field: SerializeField] public int LargeCellMaxRows { get; private set; } = 5;
		[field: SerializeField] public int CartLineLimit { get; private set; } = 99;
		[field: SerializeField] public int CursorBreathMilliseconds { get; private set; } = 550;
		[field: SerializeField] public int PopVisibleMilliseconds { get; private set; } = 800;
		[field: SerializeField] public int PopFadeMilliseconds { get; private set; } = 200;
		[field: SerializeField] public int FocusGuardMilliseconds { get; private set; } = 200;
		[field: SerializeField] public float CursorOutset { get; private set; } = 2f;
		[field: SerializeField] public float PopWalletGap { get; private set; } = 24f;
		[field: SerializeField] public float PopWalletDrop { get; private set; } = 8f;
	}
}
