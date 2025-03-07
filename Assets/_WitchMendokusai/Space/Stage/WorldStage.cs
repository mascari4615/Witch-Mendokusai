using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "WS_", menuName = "Data/" + nameof(WorldStage))]
	public class WorldStage : Stage
	{
		[field: SerializeField] public int Temp { get; private set; }
	}
}