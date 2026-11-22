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
	}
}
