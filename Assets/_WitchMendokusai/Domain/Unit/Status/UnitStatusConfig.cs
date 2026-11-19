using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 상태이상 몸 효과의 움직임 꼴
	public enum UnitStatusEffectMotion
	{
		None = 0,
		Rise = 1,
		Orbit = 2,
	}

	[Serializable]
	public class UnitStatusStyle
	{
		public UnitStatusType Type;

		[Header("Rule")]
		[Min(0f), Tooltip("기본 지속 초. 거는 쪽이 따로 주지 않을 때")] public float Seconds = 3f;
		[Range(0f, 1f), Tooltip("보스가 받는 지속 시간 비율. 0이면 보스 면역")] public float BossRatio = 1f;
		[Min(0f), Tooltip("틱 피해 간격 초. 0이면 피해 없음")] public float TickSeconds;
		[Min(0f), Tooltip("틱 피해 한 번, 최대 체력의 퍼센트")] public float TickMaxHealthPercent;
		[Min(0), Tooltip("틱 피해 한 번의 최소값")] public int TickMinDamage;
		[Range(0f, 1f), Tooltip("이동 속도를 줄이는 비율. 둔화만 씀")] public float SlowRatio;
		[Min(1), Tooltip("쌓임 상한. 1이면 쌓이지 않음. 다시 걸릴 때마다 하나씩 쌓이고 틱 피해가 쌓임 수만큼 곱해짐")] public int MaxStacks = 1;
		[Min(0f), Tooltip("걸린 동안 받는 피해 증가 퍼센트 (표식). 상태이상 틱에는 적용 안 함")] public float DamageTakenPercent;
		[Min(0), Tooltip("틱마다 옆 몬스터로 튀는 수 (감전). 0이면 안 튐")] public int ChainCount;
		[Min(0f), Tooltip("튀는 거리, 월드 단위")] public float ChainRadius = 4.5f;
		[Min(0f), Tooltip("튀는 피해, 틱 피해의 퍼센트")] public float ChainDamagePercent = 50f;
		[Tooltip("걸린 동안 이동 방향을 플레이어 반대로 덮어씀 (공포)")] public bool FleeFromPlayer;
		[Tooltip("걸린 동안 스킬 사용 봉쇄. 쿨타임은 흐름 (침묵)")] public bool BlockSkills;
		[Tooltip("걸린 동안 그림 움직임 정지")] public bool StopAnimation;
		[Range(0f, 1f), Tooltip("걸린 동안 피격 흔들림 세기 배율")] public float HitShakeScale = 1f;
		[Min(0f), Tooltip("걸린 동안 피격 흔들림 빠르기 배율")] public float HitShakeFrequencyScale = 1f;

		[Header("Icon")]
		[Tooltip("흰 실루엣 + 검은 테 그림. 머리 위 칸 안과 틱 숫자 옆에 같이 씀. 흰 부분만 색으로 물들임")] public Sprite Icon;
		[Tooltip("상태색. 실루엣, 남은 시간 테두리, 틱 숫자 옆 아이콘")] public Color IconColor = Color.white;

		[Header("Body")]
		[Tooltip("몸 물들임 색. 알파가 세기")] public Color Tint = new(1f, 1f, 1f, 0f);
		[Min(0f), Tooltip("물들임 깜빡임, 초당 횟수. 0이면 고정")] public float TintPulseRate;
		[Range(0f, 1f), Tooltip("깜빡임 깊이")] public float TintPulseDepth;
		[Tooltip("그림 가장자리 테두리 색. 알파 0이면 없음")] public Color Rim = new(1f, 1f, 1f, 0f);
		[Range(0f, 1f), Tooltip("움직일 때 뒤에 남는 잔상 투명도. 0이면 없음")] public float GhostAlpha;
		[Min(0.01f), Tooltip("잔상 지연 초")] public float GhostLagSeconds = 0.12f;

		[Header("Small effect")]
		public UnitStatusEffectMotion EffectMotion = UnitStatusEffectMotion.None;
		public Sprite EffectSprite;
		public Color EffectColor = Color.white;
		[Min(0), Tooltip("동시에 보이는 개수")] public int EffectCount;
		[Min(0f), Tooltip("한 개 크기")] public float EffectSize = 0.2f;
		[Min(0f), Tooltip("초당 한 바퀴 수")] public float EffectRate = 1f;
		[Min(0f), Tooltip("떠오름은 퍼지는 폭, 공전은 반지름. 몸 크기 비율")] public float EffectSpread = 0.5f;

		[Header("Tick number")]
		[Tooltip("틱 피해 숫자 색. 크리티컬처럼 일반 피해와 구분")] public Color TickTextColor = Color.white;
		[Min(0.1f), Tooltip("틱 피해 숫자 크기, 일반 숫자 크기 비율")] public float TickTextScale = 0.85f;
		[Min(0f), Tooltip("틱 피해 숫자가 떠오르는 높이 배율")] public float TickTextRiseScale = 1f;
		[Min(0f), Tooltip("좌우로 흔들리는 폭, 화면 단위")] public float TickTextSway;
		[Min(0f), Tooltip("좌우 흔들림, 초당 왕복 수")] public float TickTextSwayRate;
		[Range(0f, 1f), Tooltip("밝기 깜빡임 깊이")] public float TickTextFlicker;
		[Min(0f), Tooltip("밝기 깜빡임, 초당 횟수")] public float TickTextFlickerRate;
	}

	// 상태이상 규칙과 표시. 같은 종류는 한 줄
	[CreateAssetMenu(fileName = nameof(UnitStatusConfig), menuName = "WM/Variable/" + nameof(UnitStatusConfig))]
	public class UnitStatusConfig : ScriptableObject
	{
		[Tooltip("위 줄이 몸 물들임 우선")] public List<UnitStatusStyle> Styles = new();

		[Header("View")]
		[Min(0), Tooltip("아이콘과 몸 효과를 동시에 보이는 유닛 수 상한. 넘으면 물들임만")] public int MaxViews = 256;
		[Tooltip("아이콘, 효과 그림 재질. 카메라를 보는 재질")] public Material OverlayMaterial;
		[Min(1), Tooltip("머리 위 한 줄에 보이는 칸 수 상한. 넘는 상태이상은 효과만 있고 칸은 없음")] public int IconSlots = 6;
		[Min(0.01f), Tooltip("칸 한 변 길이. 1080p 에서 약 26px 이 되는 값")] public float IconSize = 0.43f;
		[Min(0f), Tooltip("그림 위 끝과 아이콘 사이 간격")] public float IconGap = 0.08f;
		[Min(0f), Tooltip("아이콘 사이 간격, 아이콘 크기 비율")] public float IconSpacing = 1.1f;
		[Min(0f), Tooltip("아이콘이 처음 뜰 때 튀는 시간 초")] public float IconPopSeconds = 0.18f;
		[Min(0f), Tooltip("튈 때 커지는 비율")] public float IconPopScale = 0.35f;
		[Min(0.0001f), Tooltip("표시 정렬 거리 차이. 몸 그림 정렬점 (발밑) 기준 카메라 쪽으로")] public float SortBias = 0.03f;
		[Min(0f), Tooltip("테두리 두께, 그림 화소")] public float RimWidth = 2.5f;
		[Min(1), Tooltip("한 프레임에 새로 만드는 표시 묶음 수. 첫 얼음 때 끊김 방지")] public int ViewsCreatedPerFrame = 12;

		[Header("Icon cell (RoR2식 칸)")]
		[Tooltip("어두운 칸 그림. 색은 CellColor")] public Sprite CellBack;
		public Color CellColor = new(0.1f, 0.08f, 0.13f, 0.94f);
		[Tooltip("남은 시간 테두리 그림 묶음. 번호 0이 가장 짧음, 마지막이 가득. 한 칸 한 장, 왼쪽 위부터 행 순서. 시계 방향으로 줄어듦")] public Texture2D RingSheet;
		[Min(1)] public int RingColumns = 8;
		[Min(1)] public int RingFrames = 48;
		[Tooltip("숫자 0부터 9까지 가로 한 줄. 쌓임 수 (오른쪽 아래). 9 넘으면 9")] public Texture2D DigitSheet;
		[Range(0.2f, 1f), Tooltip("실루엣 크기, 칸 한 변 비율")] public float SilhouetteScale = 0.78f;
		[Range(0.2f, 1f), Tooltip("쌓임 숫자 한 변, 칸 한 변 비율")] public float StackDigitScale = 0.62f;
		[Min(0f), Tooltip("칸 안 겹치는 그림끼리 앞뒤를 가르는 정렬 거리. SortBias 보다 훨씬 작게")] public float LayerBias = 0.0015f;

		[Header("Small effect shape")]
		[Min(0f), Tooltip("별이 머리를 돌 때 아이콘을 더 올리는 높이, 아이콘 크기 비율")] public float OrbitIconLift = 0.6f;
		[Range(0f, 1f), Tooltip("공전 원의 세로 납작함. 1이면 화면에서 원")] public float OrbitSquash = 0.3f;
		[Range(0f, 1f), Tooltip("공전 중 앞뒤 크기 변화")] public float OrbitSizeWobble = 0.2f;
		[Range(0f, 1f), Tooltip("떠오름 시작 높이, 몸 높이 비율")] public float RiseStart = 0.2f;
		[Min(0f), Tooltip("떠오름 이동 높이, 몸 높이 비율")] public float RiseHeight = 0.85f;
		[Range(0f, 1f), Tooltip("떠오르며 작아지는 비율")] public float RiseShrink = 0.45f;

		[Header("Tick number icon")]
		[Min(0f), Tooltip("틱 피해 숫자 옆 아이콘 크기, 글자 크기 비율. 1080p 에서 약 18px")] public float TickTextIconSize = 0.95f;
		[Min(0f), Tooltip("숫자와 아이콘 사이 간격, 글자 크기 비율")] public float TickTextIconGap = 0.15f;

		[Header("Hit shake")]
		[Min(0f), Tooltip("피격 때 그림이 좌우로 흔들리는 폭")] public float HitShakeAmplitude = 0.08f;
		[Min(0f), Tooltip("연속 피격 때 쌓이는 폭 상한")] public float HitShakeMaxAmplitude = 0.14f;
		[Min(0.01f), Tooltip("흔들림 시간 초")] public float HitShakeSeconds = 0.12f;
		[Min(0f), Tooltip("초당 왕복 수")] public float HitShakeFrequency = 22f;

		public UnitStatusStyle Style(UnitStatusType type)
		{
			for (int i = 0; i < Styles.Count; i++)
				if (Styles[i].Type == type)
					return Styles[i];
			throw new InvalidOperationException($"{name} 에 {type} 줄 없음");
		}
	}
}
