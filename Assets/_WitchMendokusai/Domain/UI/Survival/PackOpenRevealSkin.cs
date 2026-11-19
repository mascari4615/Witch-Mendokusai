using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 팩 개봉. 꾸러미나 상자가 떨며 등급 색 빛이 새고, 터지면 뒷면 카드가 날아가 자리 잡고, 차례로 뒤집힘
	// - 공개 전 등급 예고: 새는 빛 색, 뒷면 오라
	// - 영웅: 다른 카드 어둡게, 카메라 더 당김, 감기 뒤 뒤집기, 금빛 번짐과 동전
	// 시안 memo wm/design/card-reveal-refs-20261118 drafts/A-pack-open.html
	[CreateAssetMenu(fileName = "RevealSkin_PackOpen", menuName = "WM/UI/" + nameof(PackOpenRevealSkin))]
	public sealed class PackOpenRevealSkin : CardRevealSkin
	{
		[field: Header("Source")]
		[field: SerializeField] public Color PackGold { get; private set; } = new(0.79f, 0.66f, 0.35f, 1f);
		[field: SerializeField] public Color PackBody { get; private set; } = new(0.29f, 0.21f, 0.4f, 1f);
		[field: SerializeField, Min(1f)] public float ChestSize { get; private set; } = 260f;
		[field: SerializeField, Min(0f)] public float SourceInAt { get; private set; } = 0.08f;
		[field: SerializeField, Min(0.01f)] public float SourceIn { get; private set; } = 0.22f;
		[field: SerializeField, Range(0.05f, 1f)] public float SourceStartScale { get; private set; } = 0.4f;

		[field: Header("Charge")]
		[field: SerializeField, Min(0f)] public float ChargeAt { get; private set; } = 0.3f;
		[field: SerializeField, Min(0.01f)] public float BurstAt { get; private set; } = 0.82f;
		[field: SerializeField, Tooltip("떨림 틱 시각, 모으기 시작부터")] public float[] Ticks { get; private set; } = { 0f, 0.16f, 0.28f, 0.37f, 0.43f, 0.47f };
		[field: SerializeField, Min(0f)] public float TrembleMax { get; private set; } = 11f;
		[field: SerializeField, Min(0f)] public float TrembleAngle { get; private set; } = 4f;
		[field: SerializeField, Min(0f), Tooltip("떨림 빠르기")] public float TrembleSpeed { get; private set; } = 24f;
		[field: SerializeField, Min(0f), Tooltip("떨림 끝 부풀기")] public float ChargeSwell { get; private set; } = 0.06f;
		[field: SerializeField, Tooltip("희귀 이상 예고 번쩍, 모으기 비율")] public float[] TeaseAt { get; private set; } = { 0.45f, 0.75f };
		[field: SerializeField, Range(0f, 1f)] public float TeasePeak { get; private set; } = 0.55f;
		[field: SerializeField, Min(0.01f)] public float TeaseTime { get; private set; } = 0.2f;

		[field: Header("Burst")]
		[field: SerializeField, Min(0f)] public float BurstBloomRadius { get; private set; } = 740f;
		[field: SerializeField, Range(0f, 1f), Tooltip("일반만일 때 번짐 반경 비율")] public float CommonBloomRatio { get; private set; } = 0.5f;
		[field: SerializeField, Range(0f, 1f)] public float BurstPeak { get; private set; } = 0.9f;
		[field: SerializeField, Min(0.01f)] public float BurstBloomTime { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f)] public float BurstShake { get; private set; } = 12f;
		[field: SerializeField, Min(0.01f)] public float BurstShakeTime { get; private set; } = 0.32f;
		[field: SerializeField, Min(0f)] public float RingSize { get; private set; } = 500f;
		[field: SerializeField, Min(1f)] public float RingScale { get; private set; } = 2.4f;
		[field: SerializeField, Min(0.01f)] public float RingTime { get; private set; } = 0.45f;
		[field: SerializeField, Min(0.01f)] public float SourceOut { get; private set; } = 0.16f;
		[field: SerializeField, Min(1f)] public float SourceOutScale { get; private set; } = 1.25f;
		[field: SerializeField] public RevealBurst BurstSparks { get; private set; } = new(70, 1080f, 300f, 0.8f, 6f);
		[field: SerializeField] public RevealBurst BurstGlow { get; private set; } = new(35, 650f, 0f, 0.5f, 5f, 0f);

		[field: Header("Fly")]
		[field: SerializeField, Min(0.01f)] public float Fly { get; private set; } = 0.42f;
		[field: SerializeField, Min(0f)] public float FlyStagger { get; private set; } = 0.035f;
		[field: SerializeField, Range(0.1f, 1f)] public float FlyStartScale { get; private set; } = 0.55f;
		[field: SerializeField, Min(0f)] public float FlyTilt { get; private set; } = 18f;
		[field: SerializeField, Min(0f)] public float AuraIn { get; private set; } = 0.25f;
		[field: SerializeField, Range(0f, 1f)] public float RareAuraAlpha { get; private set; } = 0.55f;
		[field: SerializeField, Range(0f, 1f)] public float EpicAuraAlpha { get; private set; } = 0.75f;

		[field: Header("Flip")]
		[field: SerializeField, Min(0f), Tooltip("착지 뒤 첫 뒤집기까지")] public float FlipDelay { get; private set; } = 0.08f;
		[field: SerializeField, Min(0.01f)] public float Flip { get; private set; } = 0.28f;
		[field: SerializeField, Range(0.5f, 1f), Tooltip("앞면이 보이는 시점, 뒤집기 비율")] public float RevealRatio { get; private set; } = 0.6f;
		[field: SerializeField, Min(0f)] public float Gap { get; private set; } = 0.2f;
		[field: SerializeField, Min(0f)] public float RareGap { get; private set; } = 0.3f;
		[field: SerializeField, Min(0f)] public float Lift { get; private set; } = 16f;
		[field: SerializeField, Min(1f)] public float RareLiftRatio { get; private set; } = 1.4f;
		[field: SerializeField, Min(0.01f)] public float LiftUp { get; private set; } = 0.14f;
		[field: SerializeField, Min(0.01f)] public float LiftDown { get; private set; } = 0.26f;
		[field: SerializeField, Min(1f)] public float CommonPunch { get; private set; } = 1.05f;
		[field: SerializeField, Min(1f)] public float RarePunch { get; private set; } = 1.09f;
		[field: SerializeField, Min(0.01f)] public float PunchUp { get; private set; } = 0.08f;
		[field: SerializeField, Min(0.01f)] public float PunchDown { get; private set; } = 0.18f;
		[field: SerializeField, Min(0f)] public float RareBloomRadius { get; private set; } = 410f;
		[field: SerializeField, Range(0f, 1f)] public float RarePeak { get; private set; } = 0.7f;
		[field: SerializeField, Min(0f)] public float RareShake { get; private set; } = 5f;
		[field: SerializeField] public RevealBurst CommonSparks { get; private set; } = new(10, 460f, 480f, 0.5f, 4f);
		[field: SerializeField] public RevealBurst RareSparks { get; private set; } = new(34, 840f, 480f, 0.8f, 6f);
		[field: SerializeField, Min(0.01f)] public float SweepTime { get; private set; } = 0.55f;
		[field: SerializeField, Min(1f), Tooltip("번짐, 고리 크기 기준 카드 높이. 더 작은 칸은 비례해 줄임")] public float ReferenceCardHeight { get; private set; } = 590f;

		[field: Header("Epic")]
		[field: SerializeField, Min(0f)] public float EpicHold { get; private set; } = 0.5f;
		[field: SerializeField, Range(0f, 1f)] public float OtherShade { get; private set; } = 0.55f;
		[field: SerializeField, Min(0f)] public float EpicLift { get; private set; } = 36f;
		[field: SerializeField, Min(0f)] public float EpicTremble { get; private set; } = 2.2f;
		[field: SerializeField, Min(1f)] public float EpicAuraSwell { get; private set; } = 1.18f;
		[field: SerializeField, Min(0)] public int InwardWaves { get; private set; } = 10;
		[field: SerializeField] public RevealBurst Inward { get; private set; } = new(4, 2.6f, 0f, 0.36f, 5f, 1f);
		[field: SerializeField, Tooltip("빨려드는 빛 시작 거리 최소, 최대")] public Vector2 InwardRadius { get; private set; } = new(310f, 410f);
		[field: SerializeField, Range(0.3f, 1f), Tooltip("영웅 뒤집기 길이 비율")] public float EpicFlipRatio { get; private set; } = 0.8f;
		[field: SerializeField, Min(1f)] public float EpicPunch { get; private set; } = 1.16f;
		[field: SerializeField, Min(0f)] public float EpicBloomRadius { get; private set; } = 560f;
		[field: SerializeField, Min(0.01f)] public float EpicBloomTime { get; private set; } = 0.7f;
		[field: SerializeField, Min(0f)] public float EpicShake { get; private set; } = 15f;
		[field: SerializeField] public RevealBurst EpicSparks { get; private set; } = new(90, 1320f, 600f, 1.1f, 7f);
		[field: SerializeField, Min(0)] public int EpicCoins { get; private set; } = 36;
		[field: SerializeField, Tooltip("동전 첫 속도. x 는 좌우 최대, y 는 위로 최소와 최대")] public Vector3 CoinVelocity { get; private set; } = new(780f, 360f, 1200f);
		[field: SerializeField, Min(0f)] public float CoinGravity { get; private set; } = 1800f;
		[field: SerializeField, Min(0.01f)] public float CoinLife { get; private set; } = 1.6f;
		[field: SerializeField, Min(0f)] public float CoinSize { get; private set; } = 8f;
		[field: SerializeField, Min(0)] public int CoinSounds { get; private set; } = 8;
		[field: SerializeField, Min(0.01f)] public float CoinSoundGap { get; private set; } = 0.055f;
		[field: SerializeField, Min(0)] public int EpicTwinkles { get; private set; } = 18;
		[field: SerializeField, Tooltip("반짝임 수명 최소, 최대")] public Vector2 TwinkleLife { get; private set; } = new(1.6f, 2.6f);
		[field: SerializeField, Min(0f)] public float EpicSettle { get; private set; } = 0.75f;

		public override void Build(CardRevealRun run)
		{
			Color top = run.TierColor(run.Best);
			Vector2 cardSize = run.Cards[0].Rect.rect.size;
			RevealSource source = run.ChestSprite != null
				? RevealSource.Chest(run, run.ChestSprite, ChestSize)
				: RevealSource.Pack(run, cardSize, PackGold, PackBody);
			Image leak = run.Stage.NewImage(false, RevealTextures.Ring, new Color(top.r, top.g, top.b, 0f), run.Origin, source.Size * AuraSizeRatio, "[Reveal] Leak");
			leak.transform.SetSiblingIndex(source.Root.GetSiblingIndex());

			// 1. 모으기: 나타나서 점점 세게 떨고 등급 색 빛이 샘
			source.Root.localScale = Vector3.one * SourceStartScale;
			run.Play(SourceInAt, source.Root.DOScale(1f, SourceIn).SetEase(Ease.OutBack));
			float charge = BurstAt - ChargeAt;
			Vector2 home = source.Root.anchoredPosition;
			run.Play(ChargeAt, DOVirtual.Float(0f, 1f, charge, progress =>
			{
				float strength = 1f + progress * progress * TrembleMax;
				float phase = progress * TrembleSpeed;
				source.Root.anchoredPosition = home + new Vector2(Mathf.Sin(phase * 2.7f), Mathf.Cos(phase * 3.1f) * 0.5f) * strength;
				source.Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 1.9f) * strength * TrembleAngle / TrembleMax);
				source.Root.localScale = Vector3.one * (1f + progress * ChargeSwell);
			}));
			run.Play(ChargeAt, leak.DOFade(1f, charge).SetEase(Ease.InQuad));
			run.Sfx(ChargeAt, WhooshSfx);
			foreach (float tick in Ticks)
				run.Sfx(ChargeAt + tick, TickSfx);
			if (run.Best != CardRevealTier.Common)
				foreach (float tease in TeaseAt)
					run.Bloom(ChargeAt + charge * tease, run.Origin, RareBloomRadius, top, TeasePeak, TeaseTime);

			// 2. 터짐: 뜯기거나 뚜껑 열림, 빛 번짐, 고리, 흔들림, 입자
			source.Open(run, BurstAt);
			run.Play(BurstAt, source.Group.DOFade(0f, SourceOut));
			run.Play(BurstAt, source.Root.DOScale(SourceOutScale, SourceOut).SetEase(Ease.OutQuad));
			run.Play(BurstAt, leak.DOFade(0f, SourceOut));
			bool commonOnly = run.Best == CardRevealTier.Common;
			run.Bloom(BurstAt, run.Origin, commonOnly ? BurstBloomRadius * CommonBloomRatio : BurstBloomRadius, top, commonOnly ? BurstPeak * CommonBloomRatio : BurstPeak, BurstBloomTime);
			// 일반만이면 고리 없음. 흰 회색 고리는 화면을 씻는 값싼 느낌
			if (run.Best != CardRevealTier.Common)
				run.Ring(BurstAt, run.Origin, RingSize, RingScale, top, RingTime);
			run.Shake(BurstAt, BurstShake, BurstShakeTime);
			run.Sfx(BurstAt, BurstSfx);
			run.Burst(BurstAt, run.Origin, BurstSparks, top);
			run.Burst(BurstAt, run.Origin, BurstGlow, Color.white);

			// 3. 뒷면 카드가 날아가 자리 잡기
			for (int i = 0; i < run.Cards.Count; i++)
				FlyIn(run, run.Cards[i], i, BurstAt + i * FlyStagger);
			float landed = BurstAt + Fly + FlyStagger * (run.Cards.Count - 1);
			foreach (CardRevealCard card in run.Cards)
				if (card.Tier != CardRevealTier.Common)
					run.Play(landed, card.Aura.DOFade(card.Tier == CardRevealTier.Epic ? EpicAuraAlpha : RareAuraAlpha, AuraIn));

			// 4. 차례 뒤집기
			// 감기는 마지막 영웅 한 장만. 영웅이 여럿이면 앞의 것은 희귀처럼 빠르게 (상자 보상 5칸이 모두 영웅이어도 길어지지 않게)
			int lastEpic = -1;
			for (int i = 0; i < run.Cards.Count; i++)
				if (run.Cards[i].Tier == CardRevealTier.Epic)
					lastEpic = i;
			float nextAt = landed + FlipDelay;
			for (int i = 0; i < run.Cards.Count; i++)
				nextAt = i == lastEpic ? FlipEpic(run, i, nextAt) : FlipNormal(run, run.Cards[i], nextAt);
			run.Mark(nextAt);
		}

		private void FlyIn(CardRevealRun run, CardRevealCard card, int index, float at)
		{
			RectTransform rect = card.Rect;
			Vector3 from = run.Stage.ToParentLocal(run.Origin, rect.parent);
			float tilt = (index - (run.Cards.Count - 1) * 0.5f + Random.Range(-0.3f, 0.3f)) * FlyTilt;
			rect.localPosition = from;
			rect.localScale = card.HomeScale * FlyStartScale;
			rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
			run.Play(at, card.Group.DOFade(1f, PunchUp));
			run.Play(at, rect.DOLocalMove(card.HomePosition, Fly).SetEase(Ease.OutCubic));
			run.Play(at, rect.DOScale(card.HomeScale, Fly).SetEase(Ease.OutBack));
			run.Play(at, rect.DOLocalRotateQuaternion(card.HomeRotation, Fly).SetEase(Ease.OutCubic));
			run.Sfx(at + Fly, LandSfx);
		}

		private float FlipNormal(CardRevealRun run, CardRevealCard card, float at)
		{
			// 감기 없는 영웅도 희귀 박자에 영웅 색과 소리
			bool rare = card.Tier != CardRevealTier.Common;
			RectTransform rect = card.Rect;
			Vector3 home = card.HomePosition;
			Vector2 center = run.Stage.ToLocal(rect);
			run.Play(at, rect.DOLocalMoveY(home.y + (rare ? Lift * RareLiftRatio : Lift), LiftUp).SetEase(Ease.OutQuad));
			run.Play(at + LiftUp, rect.DOLocalMoveY(home.y, LiftDown).SetEase(Ease.OutBack));
			float reveal = AddFlip(run, card, at, Flip);
			Color tint = run.TierColor(card.Tier);
			run.Play(reveal, rect.DOScale(card.HomeScale * (rare ? RarePunch : CommonPunch), PunchUp).SetEase(Ease.OutQuad));
			run.Play(reveal + PunchUp, rect.DOScale(card.HomeScale, PunchDown).SetEase(Ease.OutQuad));
			run.At(reveal, () => card.Sweep(SweepTime));
			run.Mark(reveal + SweepTime);
			run.Sfx(reveal, TierSfx(card.Tier));
			run.Burst(reveal, center, rare ? RareSparks : CommonSparks, tint);
			if (rare)
			{
				run.Bloom(reveal, center, CardScaled(rect, RareBloomRadius), tint, RarePeak, BurstBloomTime);
				run.Shake(reveal, RareShake, PunchDown);
				run.Sfx(reveal, BurstSfx);
			}
			return at + (rare ? RareGap : Gap);
		}

		private float FlipEpic(CardRevealRun run, int index, float at)
		{
			CardRevealCard card = run.Cards[index];
			RectTransform rect = card.Rect;
			Vector3 home = card.HomePosition;
			Vector2 center = run.Stage.ToLocal(rect);
			Color gold = run.TierColor(CardRevealTier.Epic);
			// 감기: 다른 카드 어둡게, 카메라 더 당김, 떠서 떨림, 빛이 빨려듦
			foreach (CardRevealCard other in run.Cards)
				if (other != card)
					run.Play(at, other.Shade.DOFade(OtherShade, AuraIn));
			run.CameraPunch(at, EpicCameraDistance, CameraFieldOfView * 0.5f, EpicHold);
			run.Sfx(at, WhooshSfx);
			run.Play(at, DOVirtual.Float(0f, 1f, EpicHold, progress =>
			{
				float shake = progress * EpicTremble;
				float phase = progress * TrembleSpeed;
				rect.localPosition = home + new Vector3(Mathf.Cos(phase * 2.9f) * shake, Mathf.Min(1f, progress * 3f) * EpicLift, 0f);
				rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 2.3f) * shake);
			}).SetEase(Ease.InQuad));
			run.Play(at, card.Aura.DOFade(1f, EpicHold).SetEase(Ease.InQuad));
			run.Play(at, card.Aura.rectTransform.DOScale(EpicAuraSwell, EpicHold).SetEase(Ease.InQuad));
			for (int wave = 0; wave < InwardWaves; wave++)
				run.Emit(at + wave * EpicHold / (InwardWaves + 1), () => EmitInward(run, center, gold));

			float flipAt = at + EpicHold;
			float reveal = AddFlip(run, card, flipAt, Flip * EpicFlipRatio);
			run.Play(reveal, rect.DOLocalMove(home, LiftDown).SetEase(Ease.OutBack));
			run.Play(reveal, rect.DOScale(card.HomeScale * EpicPunch, PunchUp).SetEase(Ease.OutQuad));
			run.Play(reveal + PunchUp, rect.DOScale(card.HomeScale, LiftDown).SetEase(Ease.OutBack));
			run.Bloom(reveal, center, CardScaled(rect, EpicBloomRadius), gold, 1f, EpicBloomTime);
			run.Ring(reveal, center, CardScaled(rect, RingSize), RingScale, gold, RingTime);
			run.Shake(reveal, EpicShake, EpicBloomTime);
			run.Sfx(reveal, EpicSfx);
			run.Sfx(reveal, BurstSfx);
			for (int coin = 0; coin < CoinSounds; coin++)
				run.Sfx(reveal + (coin + 1) * CoinSoundGap, CoinSfx);
			run.At(reveal, () => card.Sweep(SweepTime));
			run.Burst(reveal, center, EpicSparks, gold);
			run.Emit(reveal, () => EmitCoins(run, center, card.Rect.rect.size, gold));
			foreach (CardRevealCard other in run.Cards)
				if (other != card)
					run.Play(reveal + EpicSettle * 0.5f, other.Shade.DOFade(0f, AuraIn));
			// 광택 훑기가 끝날 때까지. 입자는 끝난 뒤에도 수명까지 남음
			run.Mark(reveal + SweepTime);
			return reveal + EpicSettle;
		}

		// 번짐과 고리는 카드 크기에 맞춤. 레벨업 카드 높이 기준값, 상자 보상 칸처럼 작으면 줄어듦
		private float CardScaled(RectTransform rect, float size) => size * Mathf.Min(1f, rect.rect.height / ReferenceCardHeight);

		// 0도에서 90도까지 돌면 뒷면을 끄고 -90도에서 0도로. 앞면이 보이는 시각을 돌려줌
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

		private void EmitInward(CardRevealRun run, Vector2 center, Color gold)
		{
			for (int index = 0; index < Inward.Count; index++)
			{
				float angle = Random.value * Mathf.PI * 2f;
				float radius = Random.Range(InwardRadius.x, InwardRadius.y);
				Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
				run.Stage.FrontParticles.Add(new UIRevealParticles.Particle
				{
					Position = center + direction * radius,
					// Speed 는 거리 배율. 수명 안에 가운데에 닿게
					Velocity = -direction * radius * Inward.Speed,
					Drag = 0.2f,
					Life = Inward.Life,
					Size = Inward.Size,
					Tint = gold,
					Kind = RevealParticleKind.Spark,
				});
			}
		}

		private void EmitCoins(CardRevealRun run, Vector2 center, Vector2 cardSize, Color gold)
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
			for (int twinkle = 0; twinkle < EpicTwinkles; twinkle++)
				particles.Add(new UIRevealParticles.Particle
				{
					Position = center + new Vector2(Random.Range(-0.6f, 0.6f) * cardSize.x, Random.Range(-0.5f, 0.5f) * cardSize.y),
					Velocity = new Vector2(0f, Random.Range(0.08f, 0.2f) * cardSize.y),
					Life = Random.Range(TwinkleLife.x, TwinkleLife.y),
					Size = CommonSparks.Size,
					Tint = gold,
					Kind = RevealParticleKind.Twinkle,
				});
		}
	}
}
