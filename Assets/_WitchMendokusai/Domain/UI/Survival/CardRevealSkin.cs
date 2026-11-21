using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum CardRevealTier
	{
		Common = 0,
		Rare = 1,
		Epic = 2
	}

	// 공개할 칸 하나. 레벨업 카드, 덱 카드, 상자 보상 칸
	public readonly struct CardRevealEntry
	{
		public readonly RectTransform Card;
		public readonly CardRevealTier Tier;

		public CardRevealEntry(RectTransform card, CardRevealTier tier)
		{
			Card = card;
			Tier = tier;
		}
	}

	// 입자 터짐 한 번의 값. 스킨 에셋에서 조정
	[Serializable]
	public struct RevealBurst
	{
		[Min(0)] public int Count;
		[Min(0f)] public float Speed;
		public float Gravity;
		[Min(0.01f)] public float Life;
		[Min(0f)] public float Size;
		[Range(0f, 1f), Tooltip("불꽃 줄 비율, 나머지는 부드러운 점")] public float SparkRatio;

		public RevealBurst(int count, float speed, float gravity, float life, float size, float sparkRatio = 0.55f)
		{
			Count = count;
			Speed = speed;
			Gravity = gravity;
			Life = life;
			Size = size;
			SparkRatio = sparkRatio;
		}
	}

	// 화면마다 쓰는 공개 연출. 테마 에셋에서 갈아낌
	[Serializable]
	public class CardRevealSkins
	{
		[field: SerializeField, Tooltip("강화 카드 고르기")] public CardRevealSkin SelectCard { get; private set; }
		[field: SerializeField, Tooltip("레벨업 첫 단계 장비 고르기")] public CardRevealSkin SelectDeck { get; private set; }
		[field: SerializeField, Tooltip("상자 보상")] public CardRevealSkin Chest { get; private set; }
	}

	// 카드 공개 연출 하나. 박자와 수치는 에셋 값, 효과는 UICardRevealStage 와 CardRevealCard 부품
	public abstract class CardRevealSkin : ScriptableObject
	{
		[field: Header("Repeat")]
		[field: SerializeField, Tooltip("처음 FullPlays 번 뒤에 쓸 스킨. 비우면 늘 이 스킨")] public CardRevealSkin RepeatSkin { get; private set; }
		[field: SerializeField, Min(0), Tooltip("이 스킨을 보여 줄 횟수. 세이브의 카드팩 풀 연출 본 횟수로 셈")] public int FullPlays { get; private set; } = 1;

		[field: Header("Tier")]
		[field: SerializeField] public Color CommonColor { get; private set; } = new(0.91f, 0.89f, 0.86f, 1f);
		[field: SerializeField] public Color RareColor { get; private set; } = new(0.373f, 0.831f, 1f, 1f);
		[field: SerializeField] public Color EpicColor { get; private set; } = new(1f, 0.82f, 0.3f, 1f);

		[field: Header("Screen")]
		[field: SerializeField, Range(0f, 1f), Tooltip("배경 어둡게 최대값")] public float DimAlpha { get; private set; } = 0.82f;
		[field: SerializeField, Min(0.01f)] public float DimIn { get; private set; } = 0.3f;
		[field: SerializeField, Min(0.1f), Tooltip("전투 카메라 거리 배율. 1 보다 작으면 당김")] public float CameraDistance { get; private set; } = 0.9f;
		[field: SerializeField, Tooltip("전투 카메라 시야각 더함 (도)")] public float CameraFieldOfView { get; private set; } = -3f;
		[field: SerializeField, Min(0.01f)] public float CameraIn { get; private set; } = 0.45f;
		[field: SerializeField, Min(0.01f)] public float CameraOut { get; private set; } = 0.4f;
		[field: SerializeField, Min(1f), Tooltip("카드 층 확대. 고르는 동안 유지되므로 1 이면 카드가 레이아웃 크기 그대로")] public float ContentZoom { get; private set; } = 1f;
		[field: SerializeField, Min(1f), Tooltip("상자 보상 카드 층 확대. 카드가 한 장이라 화면에서 작음")] public float ChestContentZoom { get; private set; } = 1.45f;
		[field: SerializeField, Min(0.1f), Tooltip("영웅 감기 때 카메라 거리 배율 (곱)")] public float EpicCameraDistance { get; private set; } = 0.92f;
		[field: SerializeField, Range(0f, 1f), Tooltip("고르는 동안 남기는 어둡게 비율")] public float HoldDimRatio { get; private set; } = 0.55f;
		[field: SerializeField, Min(0f), Tooltip("건너뛰기 직후 고르기 입력 차단 시간")] public float SkipChoiceLock { get; private set; } = 0.25f;
		[field: SerializeField, Min(0f), Tooltip("공개 시작 뒤 입력이 고르기도 건너뛰기도 하지 않는 시간. 이동 중 누른 키로 연출이 넘어가지 않게")] public float SkipGuard { get; private set; } = 0.3f;

		[field: Header("Card")]
		[field: SerializeField, Range(0f, 45f), Tooltip("카드 위 빛 줄 기울기 (도)")] public float ShineAngle { get; private set; } = 20f;
		[field: SerializeField, Range(0f, 60f), Tooltip("상자 뚜껑이 열리는 각도 (도)")] public float LidOpenAngle { get; private set; } = 24f;
		[field: SerializeField, Range(0.02f, 0.2f), Tooltip("꾸러미 위 찢김 띠 높이, 꾸러미 대비")] public float PackTearRatio { get; private set; } = 0.07f;
		[field: SerializeField, Min(1f), Tooltip("카드 뒤 오라 크기, 카드 대비")] public float AuraSizeRatio { get; private set; } = 1.55f;
		[field: SerializeField, Range(0f, 1f), Tooltip("공개 뒤 희귀 이상 오라 밝기")] public float IdleAuraAlpha { get; private set; } = 0.5f;
		[field: SerializeField, Min(0.1f), Tooltip("영웅 오라 숨쉬기 주기")] public float IdleAuraPeriod { get; private set; } = 1.2f;

		[field: Header("Sound")]
		[field: SerializeField] public UiSfxEvent TickSfx { get; private set; } = UiSfxEvent.FocusMove;
		[field: SerializeField] public UiSfxEvent WhooshSfx { get; private set; } = UiSfxEvent.WindowOpen;
		[field: SerializeField] public UiSfxEvent BurstSfx { get; private set; } = UiSfxEvent.LevelUp;
		[field: SerializeField] public UiSfxEvent LandSfx { get; private set; } = UiSfxEvent.Tab;
		[field: SerializeField] public UiSfxEvent CommonSfx { get; private set; } = UiSfxEvent.CardSelect;
		[field: SerializeField] public UiSfxEvent RareSfx { get; private set; } = UiSfxEvent.Chest;
		[field: SerializeField] public UiSfxEvent EpicSfx { get; private set; } = UiSfxEvent.Evolve;
		[field: SerializeField] public UiSfxEvent CoinSfx { get; private set; } = UiSfxEvent.Purchase;
		[field: SerializeField] public UiSfxEvent ChestSfx { get; private set; } = UiSfxEvent.Chest;

		public Color TierColor(CardRevealTier tier) => tier == CardRevealTier.Epic ? EpicColor : tier == CardRevealTier.Rare ? RareColor : CommonColor;
		public UiSfxEvent TierSfx(CardRevealTier tier) => tier == CardRevealTier.Epic ? EpicSfx : tier == CardRevealTier.Rare ? RareSfx : CommonSfx;

		// 카드 뒷면 그림. null 이면 도형 뒷면
		public virtual Sprite BackSprite(CardRevealTier tier) => null;

		// run 의 시간축에 사건과 움직임을 넣는다. 화면 정지, 카메라는 감독이 공통으로 처리
		public abstract void Build(CardRevealRun run);

		public static CardRevealTier Best(IReadOnlyList<CardRevealEntry> entries)
		{
			CardRevealTier best = CardRevealTier.Common;
			foreach (CardRevealEntry entry in entries)
				if (entry.Tier > best)
					best = entry.Tier;
			return best;
		}
	}
}
