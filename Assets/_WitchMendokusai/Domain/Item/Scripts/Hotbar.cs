using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(Hotbar), menuName = "WM/DataBuffer/Hotbar")]
	public class Hotbar : Inventory
	{
		protected override int DefaultCapacity => 9;
	}
}
