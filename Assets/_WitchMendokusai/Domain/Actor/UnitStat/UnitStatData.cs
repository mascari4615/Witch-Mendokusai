using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "USD_", menuName = "WM/Variable/" + nameof(UnitStatData))]
	public class UnitStatData : StatData<UnitStatType>
	{
	}
}