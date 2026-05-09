using UnityEngine;

namespace WitchMendokusai
{
	public enum StageType
	{
		World,
		Dungeon,
	}

	public abstract class Stage : DataSO
	{
		[field: SerializeField] public StageType Type { get; private set; }
		[field: SerializeField] public StageObject Prefab { get; private set; }
	}
}