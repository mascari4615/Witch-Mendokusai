using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "USD_", menuName = "Variable/" + nameof(UnitStatData))]
	public class UnitStatData : StatData<UnitStatType>
	{
	}
}