using UnityEngine;

	public abstract class Stage : DataSO
	{
		[field: SerializeField] public StageObject Prefab { get; private set; }
	}
}