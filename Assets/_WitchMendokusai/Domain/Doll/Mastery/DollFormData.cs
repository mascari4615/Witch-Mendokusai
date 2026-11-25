using UnityEngine;

namespace WitchMendokusai
{
	// 인물 형태 (변신) 하나. 트리 노드의 form 칸이 Key 로 가리킴. 그 카드를 받는 순간 GameManager.ApplyDollForm
	// 외형은 처음엔 몸 색과 무기 모습만 (임시). 최종 그림은 나중에 이 자산에 칸을 더함
	[CreateAssetMenu(fileName = nameof(DollFormData), menuName = "WM/Variable/" + nameof(DollFormData))]
	public class DollFormData : ScriptableObject
	{
		[field: SerializeField, Tooltip("트리 JSON form 값")] public string Key { get; private set; }
		[field: SerializeField] public string DisplayName { get; private set; }
		[field: SerializeField, Tooltip("몸 모델 색 배율. 인물 기본 BodyTint 를 대신함")] public Color BodyTint { get; private set; } = Color.white;
		[field: SerializeField, Tooltip("전용 무기 동작을 이 오브젝트로 바꿈. 비우면 그대로")] public GameObject SignatureObject { get; private set; }
		[field: SerializeField, Tooltip("이 형태가 확정하는 숙련 막대. 없으면 -1")] public int ConfirmMasteryTrack { get; private set; } = DollMastery.NO_TRACK;
		[field: SerializeField, Tooltip("형태를 받는 순간 인물 자리에서 재생하는 연출. 비우면 없음")] public CombatFx TransformFx { get; private set; }
		[field: SerializeField, Min(0f), Tooltip("연출 길이 (실제 초). 이 동안 카드 창을 미룸")] public float TransformSeconds { get; private set; } = 2.6f;
		[field: SerializeField, Min(0f), Tooltip("둘레 적을 밀어내는 반지름. 0 이면 안 밂")] public float PushRadius { get; private set; } = 3.4f;
		[field: SerializeField, Min(0f)] public float PushForce { get; private set; } = 9f;
		[field: SerializeField, Min(0f), Tooltip("밀려나는 게임 시간 (초)")] public float PushSeconds { get; private set; } = 0.35f;
		[field: SerializeField, Tooltip("이 형태를 여는 카드의 겉모습. 비우면 일반 카드")] public FormCardLook CardLook { get; private set; }
	}
}
