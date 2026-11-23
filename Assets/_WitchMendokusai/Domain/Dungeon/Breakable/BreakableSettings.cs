using System;
using UnityEngine;

namespace WitchMendokusai
{
	public enum BreakableShape
	{
		Crate = 0,
		Barrel = 1,
		Pot = 2,
		Basket = 3,
		BookStack = 4,
		PotionCrate = 5,
	}

	[Serializable]
	public class BreakableKind
	{
		public string Name;
		public BreakableShape Shape;
		[Min(0f), Tooltip("배치 가중치")] public float Weight = 1f;
		[Min(1), Tooltip("체력. 무기 피해만큼 깎임")] public int Health = 10;
		[Tooltip("기준 크기, 미터 (가로, 높이, 세로)")] public Vector3 Size = Vector3.one;
		[Range(0f, .5f), Tooltip("변주마다 크기를 흔드는 비율")] public float SizeJitter = .15f;
		[Tooltip("모양별 칸 순서. 상자: 판자, 틀, 속. 통: 판자, 띠, 뚜껑. 항아리: 흙, 무늬, 속. 바구니: 짚, 테두리, 내용물. 책 더미: 종이, 표지 여럿. 약병 상자: 판자, 틀, 마개, 병 여럿")]
		public Color[] Palette = Array.Empty<Color>();
		public BreakableTable Table;
		[Tooltip("부서질 때 쏟는 바닥 액체 (기름통, 물통). 비우면 없음")] public GoopKind Spill;
		[Min(0f), Tooltip("쏟는 웅덩이 반지름, 미터")] public float SpillRadius = 1.6f;
		[Range(0f, 1f), Tooltip("위에 소품 (약병, 책) 이 얹힌 변주의 몫. 앞쪽 변주부터 얹음")] public float TopPropChance;
		[Tooltip("얹힌 소품 색 두 칸 (본체, 마개나 종이). 비우면 소품 없음")] public Color[] TopPropPalette = Array.Empty<Color>();
		[Min(.1f), Tooltip("얹힌 소품 크기 배율")] public float TopPropScale = 1f;
	}

	// 생존 던전 바닥의 부서지는 물건. 모양 생성, 배치, 타격감, 조각
	[CreateAssetMenu(fileName = nameof(BreakableSettings), menuName = "WM/Breakable/" + nameof(BreakableSettings))]
	public class BreakableSettings : ScriptableObject
	{
		[Header("Placement")]
		[Min(4f), Tooltip("배치 칸 한 변, 미터")] public float CellSize = 16f;
		[Range(1, 4), Tooltip("주인공 주변 몇 칸까지 둘지")] public int CellRadius = 2;
		[Tooltip("칸 하나의 개수 범위, 양 끝 포함")] public Vector2Int CountPerCell = new(0, 2);
		[Min(0f), Tooltip("시작 지점 주변 빈 반경")] public float ClearRadius = 6f;
		[Min(0f), Tooltip("같은 칸 안 최소 간격")] public float MinSpacing = 3f;
		[Min(0f), Tooltip("칸 경계에서 띄울 거리")] public float CellMargin = 1f;
		public int Seed = 7151;
		[Tooltip("바닥 높이")] public float GroundHeight = 0f;

		[Header("Generation")]
		[Range(1, 16), Tooltip("종류마다 미리 만들 모양 수")] public int VariantsPerKind = 4;
		[Tooltip("URP Lit 재질. 생성한 색 띠 텍스처를 덮어씀")] public Material BaseMaterial;
		[Range(2, 16), Tooltip("색 하나의 텍스처 줄 높이, 화소")] public int StripPixels = 4;
		[Range(8, 128), Tooltip("결 텍스처 가로 화소. 작을수록 결이 굵음")] public int GrainPixels = 32;
		[Range(0f, .6f), Tooltip("나뭇결 명암 세기")] public float GrainStrength = .22f;
		[Min(.1f), Tooltip("1미터당 결 반복 수")] public float GrainPerMeter = 1.4f;
		[Range(0f, .2f), Tooltip("변주마다 색을 흔드는 정도")] public float ColorJitter = .05f;
		[Range(0f, 20f), Tooltip("변주마다 기울이는 최대 각도")] public float TiltDegrees = 5f;

		[Header("Hit feedback")]
		[Min(0f), Tooltip("맞을 때 흔들림 거리")] public float ShakeDistance = .07f;
		[Min(0f), Tooltip("맞을 때 기울어짐 각도")] public float ShakeDegrees = 9f;
		[Min(.01f), Tooltip("흔들림 시간 초")] public float ShakeDuration = .2f;
		[Min(0f), Tooltip("초당 흔들림 횟수")] public float ShakeFrequency = 7f;
		[ColorUsage(false, true), Tooltip("맞는 순간 곱할 색")] public Color FlashColor = new(2.4f, 2.4f, 2.4f, 1f);
		[Min(0f), Tooltip("번쩍임 시간 초")] public float FlashDuration = .1f;
		public CombatSfxEvent HitSfx = CombatSfxEvent.WeaponHitLight;
		public CombatSfxEvent BreakSfx = CombatSfxEvent.MonsterPop;

		[Header("Shards")]
		[Tooltip("부서질 때 조각 수 범위")] public Vector2Int ShardCount = new(6, 9);
		[Tooltip("조각 크기 범위, 물건 크기 대비")] public Vector2 ShardScale = new(.2f, .38f);
		[Tooltip("옆으로 튀는 속도 범위")] public Vector2 ShardSpeed = new(2.5f, 5f);
		[Min(0f), Tooltip("위로 튀는 속도")] public float ShardLift = 5f;
		[Min(0f)] public float ShardGravity = 20f;
		[Range(0f, 1f), Tooltip("바닥에서 되튀는 비율")] public float ShardBounce = .3f;
		[Min(0f), Tooltip("조각 회전 속도, 초당 도")] public float ShardSpin = 540f;
		[Min(0f), Tooltip("얹힌 소품이 위로 튀는 속도")] public float TopPropLift = 6.5f;
		[Min(0f), Tooltip("얹힌 소품이 옆으로 튀는 속도")] public float TopPropSpread = 1.5f;
		[Min(0f), Tooltip("얹힌 소품 회전 속도, 초당 도")] public float TopPropSpin = 300f;

		[Header("Top prop")]
		[Range(0f, .5f), Tooltip("얹힌 소품 자리가 윗면 가운데에서 벗어나는 범위, 윗면 크기 대비")] public float TopPropOffset = .25f;
		[Range(0f, 1f), Tooltip("얹힌 소품이 약병일 확률. 나머지는 책")] public float TopPropBottleChance = .5f;
		[Tooltip("약병 크기, 미터 (높이, 몸통 반지름). 종류의 소품 크기 배율을 곱함")] public Vector2 TopPropBottleSize = new(.22f, .065f);
		[Tooltip("책 크기, 미터 (가로, 두께, 세로). 종류의 소품 크기 배율을 곱함")] public Vector3 TopPropBookSize = new(.22f, .06f, .16f);

		[Header("Drops")]
		public NyangObject NyangPrefab;

		[Header("Kinds")]
		public BreakableKind[] Kinds = Array.Empty<BreakableKind>();
	}
}
