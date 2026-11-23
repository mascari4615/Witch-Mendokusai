using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// NPC 머리 위 퀘스트 표식과 발밑 이름표의 그림과 수치. 색과 글자는 Style (NPCNameplate.uss)
	/// 시안과 결정 기록: memo wm/design/npc-nameplate-20261123
	/// </summary>
	[CreateAssetMenu(fileName = nameof(NPCNameplateSettings), menuName = "WM/UI/" + nameof(NPCNameplateSettings))]
	public class NPCNameplateSettings : ScriptableObject
	{
		[field: SerializeField] public StyleSheet Style { get; private set; }

		[field: Header("표식 바탕 (마름모와 꼬리, 바깥 빛 여백 포함)")]
		[field: SerializeField] public Sprite StartBackground { get; private set; }
		[field: SerializeField] public Sprite WaitingBackground { get; private set; }
		[field: SerializeField] public Sprite RewardBackground { get; private set; }
		[field: Tooltip("시작 표식 깜빡임에 겹치는 마름모 모양 (흰색). 색은 USS")]
		[field: SerializeField] public Sprite FlashSilhouette { get; private set; }

		[field: Header("표식 아이콘")]
		[field: SerializeField] public Sprite StartIcon { get; private set; }
		[field: SerializeField] public Sprite WaitingIcon { get; private set; }
		[field: SerializeField] public Sprite RewardIcon { get; private set; }

		[field: Header("자리 (패널 픽셀)")]
		[field: Tooltip("표식 그림 위끝에서 꼬리 끝까지 높이. 이 점이 NPCMarker 위치 (머리) 에 닿음")]
		[field: SerializeField, Min(0f)] public float MarkerTailTipY { get; private set; } = 57f;
		[field: Tooltip("꼬리 끝을 머리에서 위로 더 띄우는 거리")]
		[field: SerializeField] public float MarkerLift { get; private set; } = 2f;
		[field: Tooltip("NPC 발밑에서 이름표 위끝까지 아래로 내리는 거리")]
		[field: SerializeField] public float NameDrop { get; private set; } = 4f;

		[field: Header("흔들림")]
		[field: SerializeField, Min(0f)] public float BobAmplitude { get; private set; } = 2f;
		[field: SerializeField, Min(0.1f)] public float BobPeriod { get; private set; } = 1.8f;

		[field: Header("거리에 따른 흐려짐 (플레이어와 NPC 사이, m)")]
		[field: SerializeField, Min(0f)] public float FadeStartDistance { get; private set; } = 14f;
		[field: SerializeField, Min(0f)] public float FadeEndDistance { get; private set; } = 20f;

		[field: Header("시작 표식 깜빡임")]
		[field: SerializeField, Min(0.1f)] public float StartFlashPeriod { get; private set; } = 1.6f;
		[field: Tooltip("깜빡임 가장 밝을 때 겹침 불투명도 (0 이면 깜빡이지 않음)")]
		[field: SerializeField, Range(0f, 1f)] public float StartFlashStrength { get; private set; } = 0.55f;

		public Sprite GetBackground(NPCQuestMark mark) => mark switch
		{
			NPCQuestMark.Start => StartBackground,
			NPCQuestMark.Waiting => WaitingBackground,
			NPCQuestMark.Reward => RewardBackground,
			_ => throw new System.ArgumentOutOfRangeException(nameof(mark), mark, null),
		};

		public Sprite GetIcon(NPCQuestMark mark) => mark switch
		{
			NPCQuestMark.Start => StartIcon,
			NPCQuestMark.Waiting => WaitingIcon,
			NPCQuestMark.Reward => RewardIcon,
			_ => throw new System.ArgumentOutOfRangeException(nameof(mark), mark, null),
		};
	}
}
