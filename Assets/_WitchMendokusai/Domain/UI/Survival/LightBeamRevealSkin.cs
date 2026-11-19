using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 빛기둥 소환. 빛이 모여 구슬 색이 등급만큼 오르고, 터진 뒤 짧게 멈췄다가 등급 색 빛기둥을 따라 카드가 떨어져 꽂힘
	// - 공개 전 등급 예고: 구슬 색 단계, 기둥 색이 카드보다 먼저
	// - 영웅: 기둥이 먼저 서서 버팀, 다른 카드 어둡게, 금빛 번짐과 바닥 파동
	// 시안 memo wm/design/card-reveal-refs-20261118 drafts/C-orb-summon.html
	[CreateAssetMenu(fileName = "RevealSkin_LightBeam", menuName = "WM/UI/" + nameof(LightBeamRevealSkin))]
	public sealed class LightBeamRevealSkin : CardRevealSkin
	{
		[field: Header("Gather")]
		[field: SerializeField, Tooltip("구슬 자리, 무대 가운데 기준")] public Vector2 OrbOffset { get; private set; } = new(0f, 150f);
		[field: SerializeField, Min(1f)] public float OrbSize { get; private set; } = 290f;
		[field: SerializeField, Range(0.05f, 1f)] public float OrbStartScale { get; private set; } = 0.15f;
		[field: SerializeField, Range(0f, 1f)] public float OrbAlpha { get; private set; } = 0.9f;
		[field: SerializeField, Min(0f)] public float GatherAt { get; private set; } = 0.08f;
		[field: SerializeField, Min(0.01f)] public float PopAt { get; private set; } = 0.95f;
		[field: SerializeField, Min(0.01f)] public float GatherInterval { get; private set; } = 0.03f;
		[field: SerializeField, Tooltip("빨려드는 빛. Speed 는 거리 배율 (수명 안에 가운데 닿음)")] public RevealBurst Gather { get; private set; } = new(5, 2.9f, 0f, 0.33f, 4f, 1f);
		[field: SerializeField, Min(0f)] public float GatherRadius { get; private set; } = 620f;
		[field: SerializeField, Tooltip("등급 색 단계 시각, 모으기 비율")] public float[] StepAt { get; private set; } = { 0.45f, 0.75f };
		[field: SerializeField, Min(0.01f)] public float StepTime { get; private set; } = 0.3f;
		[field: SerializeField, Min(0f)] public float JitterTime { get; private set; } = 0.35f;
		[field: SerializeField, Min(0f)] public float OrbJitter { get; private set; } = 7f;

		[field: Header("Chest")]
		[field: SerializeField, Min(1f)] public float ChestSize { get; private set; } = 240f;
		[field: SerializeField, Min(0.01f)] public float ChestIn { get; private set; } = 0.15f;
		[field: SerializeField, Tooltip("상자 튐 시각")] public float[] Hops { get; private set; } = { 0.16f, 0.32f, 0.46f };
		[field: SerializeField, Min(0f)] public float HopHeight { get; private set; } = 22f;
		[field: SerializeField, Min(0f), Tooltip("튈 때마다 높이 더함 비율")] public float HopGrow { get; private set; } = 0.45f;
		[field: SerializeField, Min(0.01f)] public float HopTime { get; private set; } = 0.14f;
		[field: SerializeField] public RevealBurst HopDust { get; private set; } = new(8, 360f, 360f, 0.5f, 4f);
		[field: SerializeField, Min(0f)] public float ChestOpenAt { get; private set; } = 0.62f;
		[field: SerializeField, Min(0f), Tooltip("상자일 때 모으기를 미루는 시간")] public float ChestDelay { get; private set; } = 0.55f;
		[field: SerializeField, Min(0.01f)] public float ChestOut { get; private set; } = 0.25f;

		[field: Header("Pop")]
		[field: SerializeField, Min(0f)] public float PopBloomRadius { get; private set; } = 620f;
		[field: SerializeField, Range(0f, 1f)] public float CommonBloomRatio { get; private set; } = 0.5f;
		[field: SerializeField, Min(0.01f)] public float PopTime { get; private set; } = 0.22f;
		[field: SerializeField, Min(1f)] public float PopScale { get; private set; } = 2.2f;
		[field: SerializeField, Min(0.01f)] public float PopBloomTime { get; private set; } = 0.6f;
		[field: SerializeField, Min(0f)] public float PopShake { get; private set; } = 10f;
		[field: SerializeField] public RevealBurst PopSparks { get; private set; } = new(50, 1200f, 240f, 0.7f, 6f);
		[field: SerializeField, Min(0f), Tooltip("터진 뒤 첫 기둥까지 멈춤")] public float Hold { get; private set; } = 0.22f;

		[field: Header("Beam")]
		[field: SerializeField, Min(0f)] public float BeamGap { get; private set; } = 0.18f;
		[field: SerializeField, Min(1f)] public float BeamWidth { get; private set; } = 180f;
		[field: SerializeField, Min(1f)] public float EpicBeamWidth { get; private set; } = 250f;
		[field: SerializeField, Min(0.01f)] public float BeamIn { get; private set; } = 0.1f;
		[field: SerializeField, Range(0.05f, 1f)] public float BeamStartWidth { get; private set; } = 0.6f;
		[field: SerializeField, Min(0f), Tooltip("기둥 위 끝, 무대 위 가장자리에서 내린 거리")] public float BeamTopMargin { get; private set; } = 150f;
		[field: SerializeField, Min(0f), Tooltip("기둥이 선 뒤 카드가 떨어지기까지")] public float DropDelay { get; private set; } = 0.06f;
		[field: SerializeField, Min(0f)] public float DropHeight { get; private set; } = 900f;
		[field: SerializeField, Min(0.01f)] public float Drop { get; private set; } = 0.2f;
		[field: SerializeField, Min(0.01f)] public float EpicDrop { get; private set; } = 0.26f;
		[field: SerializeField, Min(0)] public int FallWaves { get; private set; } = 5;
		[field: SerializeField] public RevealBurst FallSparks { get; private set; } = new(4, 1080f, 0f, 0.35f, 4f, 1f);
		[field: SerializeField, Tooltip("착지 찌그러짐 (가로, 세로)")] public Vector2 LandSquash { get; private set; } = new(1.06f, 0.92f);
		[field: SerializeField, Tooltip("착지 뒤 되튐 (가로, 세로)")] public Vector2 LandStretch { get; private set; } = new(0.97f, 1.03f);
		[field: SerializeField, Min(0.01f)] public float SquashTime { get; private set; } = 0.05f;
		[field: SerializeField, Min(0f)] public float LandShake { get; private set; } = 3f;
		[field: SerializeField, Min(0f)] public float RareLandShake { get; private set; } = 7f;
		[field: SerializeField, Min(0f)] public float EpicLandShake { get; private set; } = 16f;
		[field: SerializeField, Min(0.01f)] public float LandShakeTime { get; private set; } = 0.18f;
		[field: SerializeField, Min(1f)] public float GroundRingSize { get; private set; } = 360f;
		[field: SerializeField, Range(0.05f, 1f)] public float GroundRingFlatten { get; private set; } = 0.27f;
		[field: SerializeField, Tooltip("바닥 파동 배율: 일반, 희귀, 영웅")] public Vector3 GroundRingScale { get; private set; } = new(1.6f, 2.2f, 3.2f);
		[field: SerializeField, Min(0.01f)] public float BeamOut { get; private set; } = 0.35f;
		[field: SerializeField] public RevealBurst LandSparks { get; private set; } = new(10, 500f, 840f, 0.7f, 5f);
		[field: SerializeField] public RevealBurst RareLandSparks { get; private set; } = new(22, 640f, 840f, 0.7f, 5f);
		[field: SerializeField, Range(0f, 1f)] public float RareAuraAlpha { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f)] public float RareBloomRadius { get; private set; } = 400f;
		[field: SerializeField, Range(0f, 1f)] public float RarePeak { get; private set; } = 0.6f;
		[field: SerializeField, Min(0.01f)] public float SweepTime { get; private set; } = 0.55f;

		[field: Header("Epic")]
		[field: SerializeField, Min(0f)] public float EpicStand { get; private set; } = 0.4f;
		[field: SerializeField, Min(1f)] public float EpicBeamSwell { get; private set; } = 1.35f;
		[field: SerializeField, Range(0f, 1f)] public float OtherShade { get; private set; } = 0.55f;
		[field: SerializeField, Min(0)] public int RisingWaves { get; private set; } = 8;
		[field: SerializeField] public RevealBurst Rising { get; private set; } = new(3, 960f, 0f, 0.8f, 4f, 1f);
		[field: SerializeField, Range(0f, 1f)] public float EpicAuraAlpha { get; private set; } = 0.85f;
		[field: SerializeField, Range(0f, 1f), Tooltip("꽂힌 뒤 남는 기둥 밝기")] public float EpicBeamLinger { get; private set; } = 0.6f;
		[field: SerializeField, Min(0.01f)] public float EpicBeamOut { get; private set; } = 1.2f;
		[field: SerializeField, Min(0f)] public float EpicBloomRadius { get; private set; } = 580f;
		[field: SerializeField, Min(0.01f)] public float EpicBloomTime { get; private set; } = 0.8f;
		[field: SerializeField, Min(0.01f)] public float EpicShakeTime { get; private set; } = 0.5f;
		[field: SerializeField] public RevealBurst EpicLandSparks { get; private set; } = new(40, 840f, 840f, 0.7f, 5f);
		[field: SerializeField] public RevealBurst EpicSparks { get; private set; } = new(90, 1320f, 480f, 1.1f, 7f);
		[field: SerializeField, Min(0)] public int EpicTwinkles { get; private set; } = 26;
		[field: SerializeField, Tooltip("반짝임 수명 최소, 최대")] public Vector2 TwinkleLife { get; private set; } = new(1.5f, 2.5f);
		[field: SerializeField, Min(0f)] public float EpicSettle { get; private set; } = 0.3f;

		public override void Build(CardRevealRun run)
		{
			bool chest = run.ChestSprite != null;
			float offset = chest ? ChestDelay : 0f;
			if (chest)
				BuildChest(run);

			// 1. 모으기: 사방에서 빛이 빨려 들고 구슬 색이 흰색, 파랑, 금색으로 오름
			float gather = GatherAt + offset;
			float pop = PopAt + offset;
			Image orb = run.Stage.NewImage(true, RevealTextures.Bloom, new Color(1f, 1f, 1f, 0f), chest ? run.Origin : OrbOffset, Vector2.one * OrbSize, "[Reveal] Orb");
			orb.rectTransform.localScale = Vector3.one * OrbStartScale;
			run.Play(gather, orb.DOFade(OrbAlpha, StepTime));
			run.Play(gather, orb.rectTransform.DOScale(1f, pop - gather).SetEase(Ease.InQuad));
			// 상자면 구슬이 상자에서 떠올라 제자리로
			if (chest)
				run.Play(gather, orb.rectTransform.DOAnchorPos(OrbOffset, (pop - gather) * StepAt[0]).SetEase(Ease.OutCubic));
			run.Sfx(gather, WhooshSfx);
			for (float time = gather; time < pop - GatherInterval; time += GatherInterval)
				run.Emit(time, () => EmitGather(run, orb.color));
			CardRevealTier[] steps = run.Best == CardRevealTier.Epic ? new[] { CardRevealTier.Rare, CardRevealTier.Epic } : run.Best == CardRevealTier.Rare ? new[] { CardRevealTier.Rare } : new CardRevealTier[0];
			for (int step = 0; step < steps.Length && step < StepAt.Length; step++)
			{
				float time = gather + (pop - gather) * StepAt[step];
				Color tint = run.TierColor(steps[step]);
				run.Play(time, orb.DOColor(new Color(tint.r, tint.g, tint.b, OrbAlpha), BeamIn));
				run.Bloom(time, OrbOffset, OrbSize, tint, RarePeak, StepTime);
				run.Sfx(time, TierSfx(steps[step]));
			}
			Vector2 orbCenter = OrbOffset;
			run.Play(pop - JitterTime, DOVirtual.Float(0f, 1f, JitterTime, progress =>
			{
				float strength = progress * OrbJitter;
				orb.rectTransform.anchoredPosition = orbCenter + new Vector2(Mathf.Sin(progress * 40f), Mathf.Cos(progress * 52f)) * strength;
			}));

			// 2. 터짐, 그리고 짧은 멈춤
			Color top = run.TierColor(run.Best);
			run.Play(pop, orb.rectTransform.DOScale(PopScale, PopTime).SetEase(Ease.OutQuad));
			run.Play(pop, orb.DOFade(0f, PopTime));
			run.Bloom(pop, OrbOffset, run.Best == CardRevealTier.Common ? PopBloomRadius * CommonBloomRatio : PopBloomRadius, top, 1f, PopBloomTime);
			run.Shake(pop, PopShake, PopBloomTime * 0.5f);
			run.Sfx(pop, BurstSfx);
			run.Burst(pop, OrbOffset, PopSparks, top);

			// 3. 기둥과 낙하
			float nextAt = pop + Hold;
			foreach (CardRevealCard card in run.Cards)
				nextAt = DropCard(run, card, nextAt);
			run.Mark(nextAt);
		}

		private void BuildChest(CardRevealRun run)
		{
			RevealSource source = RevealSource.Chest(run, run.ChestSprite, ChestSize);
			source.Root.localScale = Vector3.one * OrbStartScale;
			run.Play(0f, source.Root.DOScale(1f, ChestIn).SetEase(Ease.OutBack));
			Color top = run.TierColor(run.Best);
			Vector2 ground = run.Origin + Vector2.down * ChestSize * 0.35f;
			for (int hop = 0; hop < Hops.Length; hop++)
			{
				source.Hop(run, Hops[hop], HopHeight * (1f + hop * HopGrow), HopTime);
				run.Sfx(Hops[hop], LandSfx);
				run.Burst(Hops[hop] + HopTime, ground, HopDust, top, true, Mathf.PI * 0.5f, Mathf.PI);
			}
			source.Open(run, ChestOpenAt);
			run.Bloom(ChestOpenAt, run.Origin, ChestSize, top, RarePeak, StepTime);
			run.Sfx(ChestOpenAt, ChestSfx);
			run.Play(ChestOpenAt + ChestDelay, source.Group.DOFade(0f, ChestOut));
		}

		private void EmitGather(CardRevealRun run, Color tint)
		{
			for (int index = 0; index < Gather.Count; index++)
			{
				float angle = Random.value * Mathf.PI * 2f;
				float radius = GatherRadius * Random.Range(0.7f, 1.5f);
				Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle) * 0.75f);
				run.Stage.BackParticles.Add(new UIRevealParticles.Particle
				{
					Position = OrbOffset + direction * radius,
					Velocity = -direction * radius * Gather.Speed,
					Drag = 0.05f,
					Life = Gather.Life,
					Size = Gather.Size,
					Tint = new Color(tint.r, tint.g, tint.b, 1f),
					Kind = RevealParticleKind.Spark,
				});
			}
		}

		private float DropCard(CardRevealRun run, CardRevealCard card, float at)
		{
			bool epic = card.Tier == CardRevealTier.Epic;
			bool rare = card.Tier == CardRevealTier.Rare;
			Color tint = run.TierColor(card.Tier);
			RectTransform rect = card.Rect;
			Vector3 home = card.HomePosition;
			Vector2 center = run.Stage.ToLocal(rect);
			float stageScale = run.Stage.Root.InverseTransformVector(rect.TransformVector(Vector3.up)).y;
			Vector2 bottom = center + Vector2.down * rect.rect.height * 0.5f * stageScale;
			float topY = run.Stage.Size.y * 0.5f - BeamTopMargin;

			// 기둥: 등급 색이 카드보다 먼저
			Image beam = run.Stage.NewImage(false, RevealTextures.Beam, new Color(tint.r, tint.g, tint.b, 0f), bottom, new Vector2(epic ? EpicBeamWidth : BeamWidth, topY - bottom.y), "[Reveal] Beam");
			beam.rectTransform.pivot = new Vector2(0.5f, 0f);
			beam.rectTransform.anchoredPosition = bottom;
			beam.rectTransform.localScale = new Vector3(BeamStartWidth, 0f, 1f);
			run.Play(at, beam.DOFade(1f, BeamIn));
			run.Play(at, beam.rectTransform.DOScale(Vector3.one, BeamIn).SetEase(Ease.OutQuad));
			run.Sfx(at, WhooshSfx);
			float stand = 0f;
			if (epic)
			{
				stand = EpicStand;
				foreach (CardRevealCard other in run.Cards)
					if (other != card)
						run.Play(at, other.Shade.DOFade(OtherShade, BeamIn));
				run.CameraPunch(at, EpicCameraDistance, CameraFieldOfView * 0.5f, stand);
				run.Play(at + BeamIn, beam.rectTransform.DOScaleX(EpicBeamSwell, stand).SetEase(Ease.InQuad));
				for (int wave = 0; wave < RisingWaves; wave++)
					run.Burst(at + BeamIn + wave * stand / (RisingWaves + 1), bottom, Rising, tint, true, Mathf.PI * 0.5f, 0.15f);
			}

			// 낙하: 불투명 카드가 늘림 없이 떨어지고 착지 때만 찌그러짐
			float dropAt = at + DropDelay + stand;
			float drop = epic ? EpicDrop : Drop;
			rect.localPosition = home + new Vector3(0f, DropHeight, 0f);
			// 이 스킨은 뒤집지 않음. 앞면으로 떨어짐
			run.At(dropAt, card.ShowFront);
			run.Play(dropAt, card.Group.DOFade(1f, SquashTime));
			run.Play(dropAt, rect.DOLocalMove(home, drop).SetEase(Ease.InQuad));
			for (int wave = 0; wave < FallWaves; wave++)
				run.Burst(dropAt + wave * drop / FallWaves, center + Vector2.up * rect.rect.height * stageScale, FallSparks, tint, true, -Mathf.PI * 0.5f, 0.1f);
			float land = dropAt + drop;
			Vector3 scale = card.HomeScale;
			run.Play(land, rect.DOScale(Vector3.Scale(scale, new Vector3(LandSquash.x, LandSquash.y, 1f)), SquashTime).SetEase(Ease.OutQuad));
			run.Play(land + SquashTime, rect.DOScale(Vector3.Scale(scale, new Vector3(LandStretch.x, LandStretch.y, 1f)), SquashTime).SetEase(Ease.OutQuad));
			run.Play(land + SquashTime * 2f, rect.DOScale(scale, SquashTime).SetEase(Ease.OutQuad));
			run.Ring(land, bottom, GroundRingSize, epic ? GroundRingScale.z : rare ? GroundRingScale.y : GroundRingScale.x, tint, epic ? EpicBloomTime : BeamOut, GroundRingFlatten);
			if (epic)
			{
				run.Play(land, beam.DOFade(EpicBeamLinger, EpicBeamOut * 0.4f).SetEase(Ease.OutQuad));
				run.Play(land + EpicBeamOut * 0.4f, beam.DOFade(0f, EpicBeamOut * 0.6f));
			}
			else
				run.Play(land, beam.DOFade(0f, BeamOut).SetEase(Ease.OutQuad));
			run.Shake(land, epic ? EpicLandShake : rare ? RareLandShake : LandShake, epic ? EpicShakeTime : LandShakeTime);
			run.Sfx(land, LandSfx);
			run.Sfx(land, TierSfx(card.Tier));
			run.At(land, () => card.Sweep(SweepTime));
			run.Mark(land + SweepTime);
			run.Burst(land, bottom, epic ? EpicLandSparks : rare ? RareLandSparks : LandSparks, tint, true, Mathf.PI * 0.5f, Mathf.PI * 0.9f);
			if (card.Tier != CardRevealTier.Common)
				run.Play(land, card.Aura.DOFade(epic ? EpicAuraAlpha : RareAuraAlpha, StepTime));
			if (rare)
			{
				run.Bloom(land, center, RareBloomRadius, tint, RarePeak, StepTime);
				run.Sfx(land, BurstSfx);
			}
			if (epic == false)
				return at + BeamGap;

			run.Bloom(land, center, EpicBloomRadius, tint, 1f, EpicBloomTime);
			run.Sfx(land, BurstSfx);
			run.Burst(land, center, EpicSparks, tint);
			run.Emit(land, () => EmitTwinkles(run, center, rect.rect.size * stageScale, tint));
			foreach (CardRevealCard other in run.Cards)
				if (other != card)
					run.Play(land + EpicSettle, other.Shade.DOFade(0f, BeamIn));
			return land + EpicSettle;
		}

		private void EmitTwinkles(CardRevealRun run, Vector2 center, Vector2 cardSize, Color tint)
		{
			for (int twinkle = 0; twinkle < EpicTwinkles; twinkle++)
				run.Stage.FrontParticles.Add(new UIRevealParticles.Particle
				{
					Position = center + new Vector2(Random.Range(-0.6f, 0.6f) * cardSize.x, Random.Range(-0.5f, 0.5f) * cardSize.y),
					Velocity = new Vector2(0f, Random.Range(0.08f, 0.2f) * cardSize.y),
					Life = Random.Range(TwinkleLife.x, TwinkleLife.y),
					Size = LandSparks.Size,
					Tint = tint,
					Kind = RevealParticleKind.Twinkle,
				});
		}
	}
}
