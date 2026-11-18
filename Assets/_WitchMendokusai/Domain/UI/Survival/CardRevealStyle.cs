using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public enum CardRevealTier
	{
		Common = 0,
		Rare = 1,
		Epic = 2
	}

	// 슬롯 회전 칸 하나. 그림과 이름
	public readonly struct CardRevealSymbol
	{
		public readonly Sprite Sprite;
		public readonly string Name;

		public CardRevealSymbol(Sprite sprite, string name)
		{
			Sprite = sprite;
			Name = name;
		}
	}

	// 회전할 카드 한 장. 멈춤 때 Icon, Label 은 회전 전 값으로 복구
	public readonly struct CardRevealEntry
	{
		public readonly Image Icon;
		public readonly TMP_Text Label;
		public readonly Transform PunchTarget;
		public readonly UIChoiceCard ChoiceCard;
		public readonly CardRevealTier Tier;
		public readonly IReadOnlyList<GameObject> Details;

		public CardRevealEntry(Image icon, TMP_Text label, Transform punchTarget, UIChoiceCard choiceCard, CardRevealTier tier, IReadOnlyList<GameObject> details)
		{
			Icon = icon;
			Label = label;
			PunchTarget = punchTarget;
			ChoiceCard = choiceCard;
			Tier = tier;
			Details = details;
		}
	}

	// 카드 등장 슬롯 연출 수치. 실제 시간 기준
	[Serializable]
	public class CardRevealStyle
	{
		[field: Header("Timing")]
		[field: SerializeField, Min(0f)] public float FirstStopDelay { get; private set; } = 0.35f;
		[field: SerializeField, Min(0f)] public float StopInterval { get; private set; } = 0.18f;
		[field: SerializeField, Min(0f), Tooltip("마지막 카드 추가 회전 시간")] public float LastStopExtraDelay { get; private set; } = 0.3f;
		[field: SerializeField, Min(0f), Tooltip("건너뛰기 직후 고르기 입력 차단 시간")] public float SkipChoiceLock { get; private set; } = 0.25f;

		[field: Header("Spin")]
		[field: SerializeField, Min(0.01f)] public float SpinStepInterval { get; private set; } = 0.045f;
		[field: SerializeField, Min(0.01f), Tooltip("멈춤 직전 칸 넘김 간격")] public float SlowStepInterval { get; private set; } = 0.11f;
		[field: SerializeField, Min(0f), Tooltip("멈춤 전 감속 구간")] public float SlowdownTime { get; private set; } = 0.22f;
		[field: SerializeField, Min(0f)] public float ScrollDistance { get; private set; } = 26f;
		[field: SerializeField, Range(0f, 1f)] public float SpinAlpha { get; private set; } = 0.55f;
		[field: SerializeField, Min(1f), Tooltip("회전 중 세로 늘림, 흐림 대용")] public float SpinStretch { get; private set; } = 1.18f;
		[field: SerializeField, Min(1)] public int MaxSymbols { get; private set; } = 8;
		[field: SerializeField, Min(0.01f)] public float SpinTickInterval { get; private set; } = 0.09f;
		[field: SerializeField] public UiSfxEvent SpinTickSfx { get; private set; } = UiSfxEvent.FocusMove;

		[field: Header("Stop")]
		[field: SerializeField, Min(1f)] public float StopPunchScale { get; private set; } = 1.12f;
		[field: SerializeField, Min(1f)] public float RareStopPunchScale { get; private set; } = 1.18f;
		[field: SerializeField, Min(1f)] public float EpicStopPunchScale { get; private set; } = 1.24f;
		[field: SerializeField, Min(0.01f)] public float StopPunchDuration { get; private set; } = 0.24f;
		[field: SerializeField, Range(0f, 1f), Tooltip("멈춤 때 아래에서 올라오는 거리 비율")] public float StopSettleRatio { get; private set; } = 0.45f;
		[field: SerializeField, Min(0.01f)] public float StopSettleDuration { get; private set; } = 0.16f;
		[field: SerializeField, Min(0.01f)] public float FlashDuration { get; private set; } = 0.3f;
		[field: SerializeField, Min(0f)] public float DetailFadeDuration { get; private set; } = 0.14f;
		[field: SerializeField] public Color FlashColor { get; private set; } = Color.white;
		[field: SerializeField] public UiSfxEvent StopSfx { get; private set; } = UiSfxEvent.Tab;
		[field: SerializeField] public UiSfxEvent RareStopSfx { get; private set; } = UiSfxEvent.Chest;
		[field: SerializeField] public UiSfxEvent EpicStopSfx { get; private set; } = UiSfxEvent.Evolve;

		[field: Header("Rare")]
		[field: SerializeField] public Color RareColor { get; private set; } = new(0.373f, 0.831f, 1f, 1f);
		[field: SerializeField] public Color EpicColor { get; private set; } = new(1f, 0.82f, 0.3f, 1f);
		[field: SerializeField, Min(0)] public int RareBurstCount { get; private set; } = 28;
		[field: SerializeField, Min(0)] public int EpicBurstCount { get; private set; } = 56;
		[field: SerializeField, Min(1)] public int RayCount { get; private set; } = 12;
		[field: SerializeField, Range(0f, 1f), Tooltip("빛줄기 하나의 폭, 칸 대비")] public float RayWidth { get; private set; } = 0.45f;
		[field: SerializeField, Min(0f), Tooltip("빛줄기 반지름, 그림 크기 대비")] public float RayRadiusRatio { get; private set; } = 1.15f;
		[field: SerializeField] public float RayRotationSpeed { get; private set; } = 70f;
		[field: SerializeField, Range(0f, 1f)] public float RayPeakAlpha { get; private set; } = 0.95f;
		[field: SerializeField, Range(0f, 1f), Tooltip("번쩍 뒤 계속 도는 빛줄기 밝기")] public float RayIdleAlpha { get; private set; } = 0.4f;
		[field: SerializeField, Range(0f, 1f), Tooltip("희귀 빛줄기 밝기 배율")] public float RareRayAlphaScale { get; private set; } = 0.7f;
		[field: SerializeField, Min(0.01f)] public float RayPeakTime { get; private set; } = 0.08f;
		[field: SerializeField, Min(0.01f)] public float RayDecayTime { get; private set; } = 0.55f;
		[field: SerializeField, Min(0f)] public float RayPopScale { get; private set; } = 1.35f;

		[field: Header("Chest")]
		[field: SerializeField, Min(0.01f)] public float ChestShakeDuration { get; private set; } = 0.55f;
		[field: SerializeField] public float ChestShakeAngle { get; private set; } = 14f;
		[field: SerializeField, Min(0f), Tooltip("초당 흔들림 횟수")] public float ChestShakeFrequency { get; private set; } = 9f;
		[field: SerializeField] public float ChestShakeOffset { get; private set; } = 10f;
		[field: SerializeField, Min(1f)] public float ChestSwellScale { get; private set; } = 1.15f;
		[field: SerializeField, Min(1f)] public float ChestIconSize { get; private set; } = 220f;
		[field: SerializeField, Range(0f, 1f)] public float ChestLeakAlpha { get; private set; } = 0.85f;
		[field: SerializeField, Min(0f)] public float ChestLeakStartScale { get; private set; } = 0.5f;
		[field: SerializeField, Min(0.01f)] public float ChestRattleInterval { get; private set; } = 0.07f;
		[field: SerializeField, Min(1f)] public float ChestPopScale { get; private set; } = 1.6f;
		[field: SerializeField, Min(0.01f)] public float ChestPopDuration { get; private set; } = 0.2f;
		[field: SerializeField, Range(0f, 1f)] public float ChestFlashAlpha { get; private set; } = 0.5f;
		[field: SerializeField, Min(0.01f)] public float ChestFlashDuration { get; private set; } = 0.3f;
		[field: SerializeField, Min(0)] public int ChestBurstCount { get; private set; } = 72;
		[field: SerializeField] public UiSfxEvent ChestOpenSfx { get; private set; } = UiSfxEvent.Chest;

		public Color TierColor(CardRevealTier tier) => tier == CardRevealTier.Epic ? EpicColor : tier == CardRevealTier.Rare ? RareColor : FlashColor;

		public UiSfxEvent TierSfx(CardRevealTier tier) => tier == CardRevealTier.Epic ? EpicStopSfx : tier == CardRevealTier.Rare ? RareStopSfx : StopSfx;

		public float TierPunch(CardRevealTier tier) => tier == CardRevealTier.Epic ? EpicStopPunchScale : tier == CardRevealTier.Rare ? RareStopPunchScale : StopPunchScale;

		public int TierBurst(CardRevealTier tier) => tier == CardRevealTier.Epic ? EpicBurstCount : tier == CardRevealTier.Rare ? RareBurstCount : 0;
	}
}
