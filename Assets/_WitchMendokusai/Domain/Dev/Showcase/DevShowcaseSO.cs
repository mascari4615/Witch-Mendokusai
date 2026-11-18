using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 진열장 바닥 아이템 종류. 값은 직렬화 정본
	public enum DevShowcaseDropType
	{
		Heal = 0,
		Magnet = 1,
		Freeze = 2,
		ClearScreen = 3,
		Nyang = 4,
		Exp = 5,
		Chest = 6,
	}

	[Serializable]
	public class DevShowcaseStatusCell
	{
		[Tooltip("칸 이름표")] public string Label;
		[Tooltip("계속 다시 거는 상태이상. 여럿이면 조합")] public List<UnitStatusType> Statuses = new();
	}

	[Serializable]
	public class DevShowcaseDropCell
	{
		[Tooltip("칸 이름표")] public string Label;
		public DevShowcaseDropType Type;
		[Min(1), Tooltip("냥 동전 하나의 값. 냥 칸만 씀")] public int NyangAmount = 10;
	}

	// 개발자 진열장 목록과 배치 수치. 새 몬스터, 상태이상, 아이템은 여기 줄 추가로 진열
	[CreateAssetMenu(fileName = nameof(DevShowcaseSO), menuName = "WM/Dev/" + nameof(DevShowcaseSO))]
	public class DevShowcaseSO : ScriptableObject
	{
		[field: Header("진입")]
		[field: SerializeField, Tooltip("진열장 판. 개발 빌드 전용 던전")] public Dungeon Dungeon { get; private set; }

		[field: Header("배치, 시작 자리 기준 미터")]
		[field: SerializeField, Tooltip("첫 줄 시작 자리 (x 오른쪽, y 앞쪽)")] public Vector2 Origin { get; private set; } = new(-14f, 8f);
		[field: SerializeField, Min(1f), Tooltip("같은 줄 칸 간격")] public float CellSpacing { get; private set; } = 4f;
		[field: SerializeField, Min(1f), Tooltip("줄 간격")] public float RowSpacing { get; private set; } = 4.5f;
		[field: SerializeField, Min(0f), Tooltip("구역 사이 추가 간격")] public float SectionGap { get; private set; } = 3f;
		[field: SerializeField, Min(0f), Tooltip("구역 제목과 첫 줄 사이")] public float SectionTitleGap { get; private set; } = 2.5f;
		[field: SerializeField, Min(1), Tooltip("한 줄 칸 수. 넘으면 다음 줄")] public int ColumnsPerRow { get; private set; } = 8;
		[field: SerializeField, Tooltip("무기 시험대 시작 자리 (x 오른쪽, y 앞쪽). 보통 뒤쪽")] public Vector2 BenchOrigin { get; private set; } = new(-10f, -8f);

		[field: Header("이름표")]
		[field: SerializeField, Tooltip("월드 글자 프리팹. 한글 글꼴이 든 것")] public LifeLabel LabelPrefab { get; private set; }
		[field: SerializeField, Tooltip("칸 이름표 자리. 칸 기준, z 음수가 카메라 쪽 (화면 아래). 머리 위면 윗줄 상태이상 아이콘과 겹침")] public Vector3 LabelOffset { get; private set; } = new(0f, 0.3f, -1.5f);
		[field: SerializeField, Tooltip("무기 받침 이름표 자리. 받침 기준")] public Vector3 PadLabelOffset { get; private set; } = new(0f, 0.3f, -1.6f);
		[field: SerializeField, Tooltip("구역 제목 자리. 제목 줄 기준")] public Vector3 SectionLabelOffset { get; private set; } = new(0f, 0.3f, 0f);
		[field: SerializeField, Min(0.1f), Tooltip("칸 이름표 크기 배율")] public float LabelScale { get; private set; } = 0.6f;
		[field: SerializeField] public Color LabelColor { get; private set; } = Color.white;
		[field: SerializeField, Min(0.1f), Tooltip("구역 제목 크기 배율")] public float SectionLabelScale { get; private set; } = 0.9f;
		[field: SerializeField] public Color SectionLabelColor { get; private set; } = new(1f, 0.85f, 0.35f, 1f);

		[field: Header("허수아비와 유지")]
		[field: SerializeField, Tooltip("상태이상 줄과 시험대에 세울 몬스터. 비우면 첫 몬스터")] public Monster DummyMonster { get; private set; }
		[field: SerializeField, Min(0.05f), Tooltip("빠진 것 다시 놓기, 자리 되돌리기 간격 초")] public float MaintenanceSeconds { get; private set; } = 0.25f;
		[field: SerializeField, Min(0.1f), Tooltip("상태이상 다시 거는 간격 초")] public float StatusRefreshSeconds { get; private set; } = 1f;
		[field: SerializeField, Min(0.1f), Tooltip("한 번 걸 때 지속 초. 다시 거는 간격보다 길게")] public float StatusSeconds { get; private set; } = 2.5f;
		[field: SerializeField, Min(0.05f), Tooltip("밀려난 몬스터를 제자리로 되돌리는 거리")] public float HomeLeash { get; private set; } = 0.8f;
		[field: SerializeField, Min(0f), Tooltip("판 시작 뒤 부서지는 물건을 처음 놓기까지 초. 바닥 정리가 먼저")] public float BreakableDelaySeconds { get; private set; } = 0.5f;

		[field: Header("상태이상 줄")]
		[field: SerializeField] public string StatusSectionTitle { get; private set; } = "상태이상";
		[field: SerializeField] public List<DevShowcaseStatusCell> StatusCells { get; private set; } = new();

		[field: Header("몬스터 줄")]
		[field: SerializeField] public string MonsterSectionTitle { get; private set; } = "몬스터";
		[field: SerializeField, Tooltip("켜면 등록된 몬스터 전부. 끄면 아래 목록만")] public bool ShowAllMonsters { get; private set; } = true;
		[field: SerializeField] public List<Monster> Monsters { get; private set; } = new();
		[field: SerializeField, Tooltip("전부 보기에서 뺄 몬스터")] public List<Monster> HiddenMonsters { get; private set; } = new();
		[field: SerializeField] public string BossSuffix { get; private set; } = " (보스)";

		[field: Header("부서지는 물건. 종류는 BreakableSettings 전부")]
		[field: SerializeField] public string BreakableSectionTitle { get; private set; } = "부서지는 물건";
		[field: SerializeField, Min(1), Tooltip("종류마다 보일 모양 수. BreakableSettings 수를 넘으면 그 수")] public int BreakableVariantsShown { get; private set; } = 4;

		[field: Header("바닥 아이템")]
		[field: SerializeField] public string DropSectionTitle { get; private set; } = "바닥 아이템";
		[field: SerializeField] public List<DevShowcaseDropCell> Drops { get; private set; } = new();
		[field: SerializeField, Min(0f), Tooltip("주운 뒤 다시 놓기까지 초")] public float DropRespawnSeconds { get; private set; } = 3f;
		[field: SerializeField, Min(0f), Tooltip("주인공이 이보다 가까우면 다시 놓지 않음. 줍기 범위보다 넓게")] public float DropRespawnDistance { get; private set; } = 5f;
		[field: SerializeField, Min(0f), Tooltip("drop 명령이 주인공 앞 몇 미터에 둘지")] public float DropDistance { get; private set; } = 2.5f;
		[field: SerializeField, Min(0f), Tooltip("drop 명령 여러 개의 옆 간격")] public float DropSpacing { get; private set; } = 1f;

		[field: Header("개발자 창 시험 탭")]
		[field: SerializeField, Min(100), Tooltip("상태 줄 갱신 간격 밀리초")] public long TabRefreshMilliseconds { get; private set; } = 500;
		[field: SerializeField, Tooltip("시간 배율 버튼 값")] public List<float> TimeScalePresets { get; private set; } = new() { 0.25f, 0.5f, 1f, 2f, 4f };

		[field: Header("무기 시험대")]
		[field: SerializeField] public string WeaponSectionTitle { get; private set; } = "무기 시험대. 받침에 서면 그 무기만";
		[field: SerializeField, Tooltip("비우면 동작 오브젝트가 있는 장비 전부")] public List<EquipmentData> Weapons { get; private set; } = new();
		[field: SerializeField] public string BareHandsLabel { get; private set; } = "맨손";
		[field: SerializeField, Min(0), Tooltip("받침 앞에 세울 허수아비 수")] public int BenchDummyCount { get; private set; } = 3;
		[field: SerializeField, Min(1f), Tooltip("허수아비 줄과 받침 줄 사이")] public float BenchDummyDistance { get; private set; } = 6f;
		[field: SerializeField, Min(0.2f), Tooltip("받침 반지름. 이 안에 서면 장착")] public float PadRadius { get; private set; } = 1.1f;
		[field: SerializeField, Min(0.01f), Tooltip("받침 두께")] public float PadThickness { get; private set; } = 0.08f;
		[field: SerializeField, Tooltip("받침 재질. 비우면 기본 도형 재질")] public Material PadMaterial { get; private set; }
	}
}
