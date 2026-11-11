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
		[field: SerializeField] public bool ShareWorldPosition { get; private set; } = true;
		[field: SerializeField, Tooltip("개발 빌드에서만 들어가는 스테이지. 릴리스에서는 이곳으로 가는 포털을 숨긴다")]
		public bool DevOnly { get; private set; }
	}
}
