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

		[field: SerializeField, Tooltip("입장 팝업과 ESC 장소 칸 이름. Name 은 에디터가 씬 Stage_{Name} 을 찾는 키라 영문 그대로 둔다. 비우면 Name")]
		public string DisplayName { get; private set; } = string.Empty;

		/// <summary> 화면에 보이는 이름. DisplayName 이 비면 Name </summary>
		public string ShownName => string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
	}
}
