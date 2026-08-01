using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) 한 스테이지의 콘텐츠 + 수치 전량 — 규칙(TowerDefenseRules) + 유닛 구성(코어/적/타워/채집건물)
	/// + 전술 프로그램 + 건설 비용 + 배치 레이아웃을 SO 인스펙터로 노출(수치 노출 룰: 하드코딩 0).
	/// TowerDefenseMatch(셸)가 이 SO 하나만 보고 스테이지를 셋업 — ArenaMatchConfig 와 동형(맵/모드/로스터가
	/// 여기선 지형/코어·적·타워·채집건물/전술·비용 3축으로 대응).
	/// </summary>
	[CreateAssetMenu(fileName = nameof(TowerDefenseStageSO), menuName = "WM/TowerDefense/TowerDefenseStageSO")]
	public class TowerDefenseStageSO : DataSO
	{
		[field: Header("_" + nameof(TowerDefenseStageSO))]
		[field: Tooltip("진행·경제 규칙 수치(웨이브 수/준비시간/자원/수입 등).")]
		[field: SerializeField] public TowerDefenseRules Rules { get; private set; }

		[field: Header("유닛 구성")]
		[field: Tooltip("수비 코어(기지) 유닛 데이터 — 파괴되면 즉시 패배.")]
		[field: SerializeField] public Unit CoreUnit { get; private set; }

		[field: Tooltip("웨이브로 스폰되는 적 유닛 데이터.")]
		[field: SerializeField] public Unit EnemyUnit { get; private set; }

		[field: Tooltip("건설 페이즈에 배치 가능한 타워 유닛 데이터.")]
		[field: SerializeField] public Unit TowerUnit { get; private set; }

		[field: Tooltip("건설 페이즈에 배치 가능한 채집건물 유닛 데이터.")]
		[field: SerializeField] public Unit HarvesterUnit { get; private set; }

		[field: Header("전술 프로그램")]
		[field: Tooltip("적 유닛의 전술(우선순위 룰 리스트) — 코어를 향해 전진·교전.")]
		[field: SerializeField] public TacticProgram EnemyTactic { get; private set; }

		[field: Tooltip("타워 유닛의 전술(우선순위 룰 리스트) — 사거리 내 적 요격.")]
		[field: SerializeField] public TacticProgram TowerTactic { get; private set; }

		[field: Header("건설 비용")]
		[field: Tooltip("타워 1기 배치 비용.")]
		[field: SerializeField, Min(0)] public int TowerCost { get; private set; }

		[field: Tooltip("채집건물 1기 배치 비용.")]
		[field: SerializeField, Min(0)] public int HarvesterCost { get; private set; }

		[field: Header("레이아웃")]
		[field: Tooltip("코어(기지) 배치 위치 — 스테이지 root 로컬 좌표.")]
		[field: SerializeField] public Vector3 CorePosition { get; private set; }

		[field: Tooltip("적 웨이브 스폰 지점(로컬 좌표) — 웨이브 적을 이 지점들에 고르게 분산 스폰.")]
		[field: SerializeField] public Vector3[] EnemySpawnPoints { get; private set; }

		[field: Tooltip("자원 채집 노드 위치(로컬 좌표) — 채집건물은 이 지점 중 하나를 점유해야만 가동(개척 리스크 = 설계 긴장).")]
		[field: SerializeField] public Vector3[] ResourceNodePositions { get; private set; }

		[field: Tooltip("채집건물 배치가 노드를 점유로 인정하는 최대 거리 — 이 반경 밖 배치는 거절(노드 스냅 X).")]
		[field: SerializeField, Min(0.5f)] public float NodeCaptureRadius { get; private set; } = 2f;

		[field: Tooltip("지면 X 축 폭(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundWidth { get; private set; } = 24f;

		[field: Tooltip("지면 Z 축 길이(월드 단위).")]
		[field: SerializeField, Min(2f)] public float GroundLength { get; private set; } = 24f;
	}
}
