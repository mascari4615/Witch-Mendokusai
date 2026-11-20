using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace WitchMendokusai
{
	// 카드팩 개봉 2판의 뒤집기
	// - 차례 뒤집기 (풀) 또는 일반 카드 한꺼번에 뒤집기 (빠른, 일괄)
	// - 영웅은 마지막 한 장만 감기 (떠서 떨림, 다른 카드 어둡게, 빨려드는 빛) 뒤 금 번짐과 동전
	public abstract partial class PackOpenV2Skin
	{
		[field: Header("Flip")]
		[field: SerializeField, Min(0f), Tooltip("착지 뒤 첫 뒤집기까지")] public float FlipDelay { get; private set; } = 0.15f;
		[field: SerializeField, Min(0.01f), Tooltip("한 장이 다시 정면이 될 때까지")] public float Flip { get; private set; } = 0.28f;
		[field: SerializeField, Range(0.5f, 1f), Tooltip("앞면 효과 시점, 뒤집기 비율")] public float RevealRatio { get; private set; } = 0.6f;
		[field: SerializeField, Tooltip("일반 카드를 한꺼번에 뒤집음. 끄면 차례로")] public bool BatchCommons { get; private set; }
		[field: SerializeField, Min(0f), Tooltip("차례 뒤집기 간격, 일반")] public float Gap { get; private set; } = 0.22f;
		[field: SerializeField, Min(0f), Tooltip("차례 뒤집기 간격, 희귀")] public float RareGap { get; private set; } = 0.34f;
		[field: SerializeField, Min(0f), Tooltip("한꺼번에 뒤집기 뒤 희귀 카드까지")] public float RareAfterBatch { get; private set; } = 0.12f;
		[field: SerializeField, Min(0f)] public float ChainRareAfterBatch { get; private set; }
		[field: SerializeField, Min(0f)] public float Lift { get; private set; } = 16f;
		[field: SerializeField, Min(0.01f)] public float LiftUp { get; private set; } = 0.12f;
		[field: SerializeField, Min(0.01f)] public float LiftDown { get; private set; } = 0.24f;
		[field: SerializeField, Min(1f)] public float CommonPunch { get; private set; } = 1.05f;
		[field: SerializeField, Min(1f)] public float RarePunch { get; private set; } = 1.1f;
		[field: SerializeField, Min(0.01f)] public float PunchUp { get; private set; } = 0.08f;
		[field: SerializeField, Min(0.01f)] public float PunchDown { get; private set; } = 0.18f;
		[field: SerializeField, Min(0f), Tooltip("희귀 번짐 반경, 카드 높이 대비")] public float RareBloomRatio { get; private set; } = 0.75f;
		[field: SerializeField, Range(0f, 1f)] public float RarePeak { get; private set; } = 0.75f;
		[field: SerializeField, Min(0f)] public float RareShake { get; private set; } = 5f;
		[field: SerializeField] public RevealBurst CommonSparks { get; private set; } = new(12, 480f, 480f, 0.5f, 4f);
		[field: SerializeField] public RevealBurst RareSparks { get; private set; } = new(34, 860f, 480f, 0.8f, 6f);
		[field: SerializeField, Min(0.01f)] public float SweepTime { get; private set; } = 0.55f;

		[field: Header("Epic")]
		[field: SerializeField, Min(0f), Tooltip("영웅 감기")] public float EpicHold { get; private set; } = 0.7f;
		[field: SerializeField, Min(0f)] public float ChainEpicHold { get; private set; } = 0.25f;
		[field: SerializeField, Range(0f, 1f)] public float OtherShade { get; private set; } = 0.55f;
		[field: SerializeField, Min(0f)] public float EpicLift { get; private set; } = 36f;
		[field: SerializeField, Min(0f)] public float EpicTremble { get; private set; } = 2.4f;
		[field: SerializeField, Min(1f)] public float EpicAuraSwell { get; private set; } = 1.2f;
		[field: SerializeField, Min(0f), Tooltip("빨려드는 빛 묶음 간격")] public float InwardGap { get; private set; } = 0.06f;
		[field: SerializeField] public RevealBurst Inward { get; private set; } = new(4, 2.6f, 0f, 0.36f, 5f, 1f);
		[field: SerializeField, Tooltip("빨려드는 빛 시작 거리, 카드 높이 대비 최소, 최대")] public Vector2 InwardRadius { get; private set; } = new(0.55f, 0.75f);
		[field: SerializeField, Min(1f)] public float EpicPunch { get; private set; } = 1.16f;
		[field: SerializeField, Min(0f), Tooltip("영웅 번짐 반경, 카드 높이 대비")] public float EpicBloomRatio { get; private set; } = 1.05f;
		[field: SerializeField, Min(0.01f)] public float EpicBloomTime { get; private set; } = 0.75f;
		[field: SerializeField, Min(0f)] public float EpicShake { get; private set; } = 15f;
		[field: SerializeField] public RevealBurst EpicSparks { get; private set; } = new(90, 1320f, 600f, 1.1f, 7f);
		[field: SerializeField, Min(0)] public int EpicCoins { get; private set; } = 36;
		[field: SerializeField, Tooltip("동전 첫 속도. x 는 좌우 최대, y 는 위로 최소와 최대")] public Vector3 CoinVelocity { get; private set; } = new(780f, 360f, 1200f);
		[field: SerializeField, Min(0f)] public float CoinGravity { get; private set; } = 1800f;
		[field: SerializeField, Min(0.01f)] public float CoinLife { get; private set; } = 1.6f;
		[field: SerializeField, Min(0f)] public float CoinSize { get; private set; } = 8f;
		[field: SerializeField, Min(0)] public int CoinSounds { get; private set; } = 8;
		[field: SerializeField, Min(0.01f)] public float CoinSoundGap { get; private set; } = 0.055f;
		[field: SerializeField, Min(0f)] public float EpicSettle { get; private set; } = 0.75f;

		private void BuildFlips(CardRevealRun run, float landed)
		{
			// 감기는 마지막 영웅 한 장만. 앞의 영웅은 희귀 박자에 영웅 색
			int lastEpic = -1;
			for (int i = 0; i < run.Cards.Count; i++)
				if (run.Cards[i].Tier == CardRevealTier.Epic)
					lastEpic = i;
			float start = landed + FlipDelay;
			if (BatchCommons == false && IsChain(run) == false)
			{
				float nextAt = start;
				for (int i = 0; i < run.Cards.Count; i++)
					nextAt = i == lastEpic ? FlipEpic(run, run.Cards[i], nextAt) : FlipNormal(run, run.Cards[i], nextAt);
				run.Mark(nextAt);
				return;
			}

			// 일반은 한꺼번에, 희귀는 그 뒤에 한꺼번에, 영웅은 마지막
			List<CardRevealCard> rares = new();
			float end = start;
			for (int i = 0; i < run.Cards.Count; i++)
			{
				CardRevealCard card = run.Cards[i];
				if (i == lastEpic)
					continue;
				if (card.Tier == CardRevealTier.Common)
					end = Mathf.Max(end, FlipNormal(run, card, start));
				else
					rares.Add(card);
			}
			bool hasCommon = rares.Count + (lastEpic >= 0 ? 1 : 0) < run.Cards.Count;
			float rareAt = hasCommon ? start + (IsChain(run) ? ChainRareAfterBatch : RareAfterBatch) : start;
			foreach (CardRevealCard rare in rares)
				end = Mathf.Max(end, FlipNormal(run, rare, rareAt));
			if (lastEpic >= 0)
				end = FlipEpic(run, run.Cards[lastEpic], end);
			run.Mark(end);
		}

		// 반환값: 다음 카드 시작 시각
		private float FlipNormal(CardRevealRun run, CardRevealCard card, float at)
		{
			bool rare = card.Tier != CardRevealTier.Common;
			RectTransform rect = card.Rect;
			Vector3 home = card.HomePosition;
			Vector2 center = run.Stage.ToLocal(rect);
			run.Play(at, rect.DOLocalMoveY(home.y + (rare ? Lift * 1.4f : Lift), LiftUp).SetEase(Ease.OutQuad));
			run.Play(at + LiftUp, rect.DOLocalMoveY(home.y, LiftDown).SetEase(Ease.OutBack));
			float reveal = AddFlip(run, card, at, Flip);
			Color tint = run.TierColor(card.Tier);
			run.Play(reveal, rect.DOScale(card.HomeScale * (rare ? RarePunch : CommonPunch), PunchUp).SetEase(Ease.OutQuad));
			run.Play(reveal + PunchUp, rect.DOScale(card.HomeScale, PunchDown).SetEase(Ease.OutQuad));
			Sweep(run, card, reveal);
			run.Sfx(reveal, TierSfx(card.Tier));
			run.Burst(reveal, center, rare ? RareSparks : CommonSparks, tint);
			if (rare)
			{
				run.Bloom(reveal, center, rect.rect.height * RareBloomRatio, tint, RarePeak, BloomTime);
				run.Shake(reveal, RareShake, PunchDown);
			}
			return at + (rare ? RareGap : Gap);
		}

		private float FlipEpic(CardRevealRun run, CardRevealCard card, float at)
		{
			RectTransform rect = card.Rect;
			Vector3 home = card.HomePosition;
			Vector2 center = run.Stage.ToLocal(rect);
			float cardHeight = rect.rect.height;
			Color gold = run.TierColor(CardRevealTier.Epic);
			float hold = IsChain(run) ? ChainEpicHold : EpicHold;
			foreach (CardRevealCard other in run.Cards)
				if (other != card)
					run.Play(at, other.Shade.DOFade(OtherShade, AuraIn));
			run.CameraPunch(at, EpicCameraDistance, CameraFieldOfView * 0.5f, Mathf.Max(0.05f, hold));
			run.Sfx(at, WhooshSfx);
			run.Play(at, DOVirtual.Float(0f, 1f, Mathf.Max(0.01f, hold), progress =>
			{
				float shake = progress * EpicTremble;
				float phase = progress * hold * 26f;
				rect.localPosition = home + new Vector3(Mathf.Cos(phase * 2.9f) * shake, Mathf.Min(1f, progress * 3f) * EpicLift, 0f);
				rect.localRotation = card.HomeRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 2.3f) * shake);
			}).SetEase(Ease.InQuad));
			run.Play(at, card.Aura.DOFade(1f, Mathf.Max(0.01f, hold)).SetEase(Ease.InQuad));
			run.Play(at, card.Aura.rectTransform.DOScale(EpicAuraSwell, Mathf.Max(0.01f, hold)).SetEase(Ease.InQuad));
			if (InwardGap > 0f)
				for (float wave = at; wave < at + hold; wave += InwardGap)
					run.Emit(wave, () => EmitInward(run, center, cardHeight, gold));

			float flipAt = at + hold;
			float reveal = AddFlip(run, card, flipAt, Flip * 0.85f);
			run.Play(reveal, rect.DOLocalMove(home, LiftDown).SetEase(Ease.OutBack));
			run.Play(reveal, rect.DOScale(card.HomeScale * EpicPunch, PunchUp).SetEase(Ease.OutQuad));
			run.Play(reveal + PunchUp, rect.DOScale(card.HomeScale, LiftDown).SetEase(Ease.OutBack));
			run.Bloom(reveal, center, cardHeight * EpicBloomRatio, gold, 1f, EpicBloomTime);
			run.Bloom(reveal, center, cardHeight * EpicBloomRatio * 0.35f, Color.white, 0.8f, EpicBloomTime * 0.7f);
			run.Ring(reveal, center, cardHeight, RingScale, gold, RingTime);
			run.Shake(reveal, EpicShake, EpicBloomTime);
			run.Sfx(reveal, EpicSfx);
			run.Sfx(reveal, BurstSfx);
			for (int coin = 0; coin < CoinSounds; coin++)
				run.Sfx(reveal + (coin + 1) * CoinSoundGap, CoinSfx);
			Sweep(run, card, reveal);
			run.Burst(reveal, center, EpicSparks, gold);
			run.Emit(reveal, () => EmitCoins(run, center));
			foreach (CardRevealCard other in run.Cards)
				if (other != card)
					run.Play(reveal + EpicSettle * 0.5f, other.Shade.DOFade(0f, AuraIn));
			run.Mark(reveal + SweepTime);
			return reveal + EpicSettle;
		}

		// 0도에서 90도, 뒷면 끄고 -90도에서 0도. 앞면 효과 시각을 돌려줌
		private float AddFlip(CardRevealRun run, CardRevealCard card, float at, float duration)
		{
			RectTransform rect = card.Rect;
			Quaternion homeRotation = card.HomeRotation;
			float half = duration * 0.5f;
			run.Play(at, DOVirtual.Float(0f, 90f, half, angle => rect.localRotation = homeRotation * Quaternion.Euler(0f, angle, 0f)).SetEase(Ease.InCubic));
			run.At(at + half, card.ShowFront);
			run.Play(at + half, DOVirtual.Float(-90f, 0f, half, angle => rect.localRotation = homeRotation * Quaternion.Euler(0f, angle, 0f)).SetEase(Ease.OutCubic));
			return at + duration * RevealRatio;
		}

		// 광택 훑기도 공개 시간축 안에서 (녹화 고정 간격과 건너뛰기 적용)
		private void Sweep(CardRevealRun run, CardRevealCard card, float at)
		{
			RectTransform shine = card.Shine;
			RectTransform band = (RectTransform)shine.GetChild(0);
			float width = card.Rect.rect.width;
			run.At(at, () => shine.gameObject.SetActive(true));
			run.Play(at, DOVirtual.Float(-width, width, SweepTime, x => band.anchoredPosition = new Vector2(x, 0f)).SetEase(Ease.OutCubic));
			run.At(at + SweepTime, () => shine.gameObject.SetActive(false));
		}

		private void EmitInward(CardRevealRun run, Vector2 center, float cardHeight, Color gold)
		{
			for (int index = 0; index < Inward.Count; index++)
			{
				float angle = Random.value * Mathf.PI * 2f;
				float radius = Random.Range(InwardRadius.x, InwardRadius.y) * cardHeight;
				Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
				run.Stage.FrontParticles.Add(new UIRevealParticles.Particle
				{
					Position = center + direction * radius,
					Velocity = -direction * radius * Inward.Speed,
					Drag = 0.2f,
					Life = Inward.Life,
					Size = Inward.Size,
					Tint = gold,
					Kind = RevealParticleKind.Spark,
				});
			}
		}

		private void EmitCoins(CardRevealRun run, Vector2 center)
		{
			UIRevealParticles particles = run.Stage.FrontParticles;
			for (int coin = 0; coin < EpicCoins; coin++)
				particles.Add(new UIRevealParticles.Particle
				{
					Position = center,
					Velocity = new Vector2(Random.Range(-CoinVelocity.x, CoinVelocity.x), Random.Range(CoinVelocity.y, CoinVelocity.z)),
					Gravity = CoinGravity,
					Drag = 0.5f,
					Life = CoinLife,
					Size = CoinSize,
					Tint = Color.white,
					Kind = RevealParticleKind.Coin,
					Spin = Random.value * Mathf.PI,
				});
		}
	}
}
