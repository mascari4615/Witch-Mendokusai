using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 카드팩 개봉 2판. 등급 팩 그림이 떨어지고, 등급 색 예고 (번짐, 고리 박동, 오르는 빛, 회전 광선), 밀어 들어가기와 떨림,
	// 종이 띠가 빠지며 폭발, 등급 뒷면 카드가 팩 입구에서 날아가 자리 잡고 뒤집힘
	// - 풀 연출과 빠른 연출은 같은 코드, 에셋 수치만 다름 (PackOpenFullSkin, PackOpenQuickSkin)
	// - 연속 레벨업 (run.Chained) 이고 ChainPath 이면 일괄 공개 길: 작은 팩, 예고 생략, 한꺼번에 뒤집기
	// 설계 memo wm/design/card-pack-v2-20261120
	public abstract partial class PackOpenV2Skin : CardRevealSkin
	{
		[field: Header("Art")]
		[field: SerializeField, Tooltip("일반, 희귀, 영웅, 전설 팩 몸통")] public Sprite[] PackArt { get; private set; } = new Sprite[4];
		[field: SerializeField, Tooltip("팩 몸통과 같은 크기의 투명 종이 띠")] public Sprite[] PackBands { get; private set; } = new Sprite[4];
		[field: SerializeField, Tooltip("등급별 뒷면 광택, 6열 4행")] public Texture2D[] BackEffects { get; private set; } = new Texture2D[4];
		[field: SerializeField, Tooltip("등급별 뒷면 광택 시간")] public Vector4 BackEffectSeconds { get; private set; } = new(0.43f, 0.64f, 0.82f, 1.08f);
		[field: SerializeField, Tooltip("일반, 희귀, 영웅, 전설 카드 뒷면")] public Sprite[] Backs { get; private set; } = new Sprite[4];
		[field: SerializeField, Range(0.05f, 0.3f), Tooltip("팩 그림 위 봉인 띠 비율")] public float TearRatio { get; private set; } = 0.11f;
		[field: SerializeField, Min(1f)] public float ChestSize { get; private set; } = 300f;

		[field: Header("Enter")]
		[field: SerializeField, Range(0.1f, 1f), Tooltip("팩 높이, 무대 높이 대비")] public float PackHeightRatio { get; private set; } = 0.6f;
		[field: SerializeField, Min(0f)] public float Enter { get; private set; } = 0.45f;
		[field: SerializeField, Tooltip("떨어지기 시작 높이, 무대 높이 대비")] public float EnterDrop { get; private set; } = 0.4f;
		[field: SerializeField, Range(0.05f, 1f)] public float EnterStartScale { get; private set; } = 0.55f;
		[field: SerializeField] public float EnterTilt { get; private set; } = -8f;

		[field: Header("Tease")]
		[field: SerializeField, Min(0f), Tooltip("등급 예고 시작")] public float TeaseAt { get; private set; } = 0.15f;
		[field: SerializeField, Min(0.01f)] public float TeaseIn { get; private set; } = 0.6f;
		[field: SerializeField, Range(0f, 1f)] public float TeaseAlpha { get; private set; } = 0.6f;
		[field: SerializeField, Min(0f), Tooltip("예고 번짐 반경, 팩 높이 대비")] public float TeaseRadiusRatio { get; private set; } = 0.85f;
		[field: SerializeField, Min(0f), Tooltip("고리 박동 간격. 0 이면 없음")] public float PulseGap { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f), Tooltip("오르는 빛 간격. 0 이면 없음")] public float MoteGap { get; private set; } = 0.12f;
		[field: SerializeField, Min(0)] public int MotesPerWave { get; private set; } = 6;
		[field: SerializeField, Tooltip("오르는 빛 속도 최소, 최대")] public Vector2 MoteSpeed { get; private set; } = new(140f, 320f);
		[field: SerializeField, Min(0.01f)] public float MoteLife { get; private set; } = 1.1f;
		[field: SerializeField, Min(0f)] public float MoteSize { get; private set; } = 7f;
		[field: SerializeField, Min(0)] public int RareRays { get; private set; } = 4;
		[field: SerializeField, Min(0)] public int EpicRays { get; private set; } = 8;
		[field: SerializeField, Tooltip("회전 광선 초당 도")] public float RaySpeed { get; private set; } = 25f;
		[field: SerializeField, Min(0f), Tooltip("광선 길이, 팩 높이 대비")] public float RayLengthRatio { get; private set; } = 1.05f;
		[field: SerializeField, Min(0f), Tooltip("광선 폭, 팩 높이 대비")] public float RayWidthRatio { get; private set; } = 0.2f;
		[field: SerializeField, Range(0f, 1f)] public float RayAlpha { get; private set; } = 0.5f;

		[field: Header("Sheen")]
		[field: SerializeField, Min(0f), Tooltip("팩 위 광택 띠 시작. 길이가 0 이면 없음")] public float SheenAt { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f)] public float SheenTime { get; private set; } = 0.5f;
		[field: SerializeField, Range(0f, 1f)] public float SheenAlpha { get; private set; } = 0.55f;

		[field: Header("Push")]
		[field: SerializeField, Min(0f), Tooltip("밀어 들어가기 시작")] public float PushAt { get; private set; } = 0.7f;
		[field: SerializeField, Min(1f)] public float PushScale { get; private set; } = 1.14f;
		[field: SerializeField, Min(0.1f), Tooltip("밀어 들어가기 카메라 거리 배율. 1 이면 없음")] public float PushCameraDistance { get; private set; } = 0.9f;
		[field: SerializeField, Range(0f, 1f)] public float PushDim { get; private set; } = 0.93f;
		[field: SerializeField, Min(0f)] public float TrembleMax { get; private set; } = 10f;
		[field: SerializeField, Min(0f)] public float TrembleAngle { get; private set; } = 3f;
		[field: SerializeField, Min(0f)] public float TrembleSpeed { get; private set; } = 26f;
		[field: SerializeField, Min(0)] public int TrembleTicks { get; private set; } = 6;
		[field: SerializeField, Min(0f), Tooltip("봉인 틈 빛 시작. 폭발보다 늦으면 폭발 0.1초 전")] public float SeamAt { get; private set; } = 1.1f;

		[field: Header("Burst")]
		[field: SerializeField, Tooltip("폭발 시각, 일반 희귀 영웅")] public Vector3 BurstAt { get; private set; } = new(1.55f, 1.95f, 2.6f);
		[field: SerializeField, Min(0f), Tooltip("큰 번짐 반경, 무대 높이 대비")] public float BloomRadiusRatio { get; private set; } = 0.55f;
		[field: SerializeField, Min(0f), Tooltip("일반만일 때 큰 번짐 반경, 무대 높이 대비")] public float CommonBloomRadiusRatio { get; private set; } = 0.4f;
		[field: SerializeField, Range(0f, 1f)] public float BloomPeak { get; private set; } = 1f;
		[field: SerializeField, Min(0.01f)] public float BloomTime { get; private set; } = 0.6f;
		[field: SerializeField, Min(0f), Tooltip("흰 가운데 번짐 반경, 무대 높이 대비")] public float WhiteRadiusRatio { get; private set; } = 0.18f;
		[field: SerializeField, Range(0f, 1f)] public float WhitePeak { get; private set; } = 0.9f;
		[field: SerializeField, Min(1f)] public float RingScale { get; private set; } = 2.6f;
		[field: SerializeField, Min(0.01f)] public float RingTime { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("흔들림 px, 일반 희귀 영웅")] public Vector3 BurstShake { get; private set; } = new(8f, 12f, 16f);
		[field: SerializeField, Min(0.01f)] public float BurstShakeTime { get; private set; } = 0.35f;
		[field: SerializeField, Tooltip("불꽃 수, 일반 희귀 영웅")] public Vector3 SparkCount { get; private set; } = new(40f, 70f, 100f);
		[field: SerializeField] public RevealBurst BurstSparks { get; private set; } = new(70, 1150f, 320f, 0.85f, 6.5f);
		[field: SerializeField] public RevealBurst BurstGlow { get; private set; } = new(30, 650f, 0f, 0.5f, 6f, 0f);
		[field: SerializeField, Min(0.01f)] public float SourceOut { get; private set; } = 0.2f;
		[field: SerializeField, Min(1f)] public float SourceOutScale { get; private set; } = 1.25f;
		[field: SerializeField, Min(0f), Tooltip("희귀 이상 빛기둥 길이. 0 이면 없음")] public float PillarTime { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f), Tooltip("빛기둥 폭, 팩 폭 대비")] public float PillarWidthRatio { get; private set; } = 0.6f;
		[field: SerializeField, Min(0.1f), Tooltip("폭발 순간 카메라 튕김 거리 배율. 1 이면 없음")] public float KickCameraDistance { get; private set; } = 0.86f;
		[field: SerializeField, Min(0f)] public float KickHold { get; private set; } = 0.12f;

		[field: Header("Fly")]
		[field: SerializeField, Min(0.01f)] public float Fly { get; private set; } = 0.45f;
		[field: SerializeField, Min(0f)] public float FlyStagger { get; private set; } = 0.06f;
		[field: SerializeField, Range(0.1f, 1f)] public float FlyStartScale { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f)] public float FlyTilt { get; private set; } = 18f;
		[field: SerializeField, Tooltip("카드가 나오는 입구, 팩 가운데에서 위로 팩 높이 대비")] public float MouthRatio { get; private set; } = 0.3f;
		[field: SerializeField, Min(0f)] public float AuraIn { get; private set; } = 0.25f;
		[field: SerializeField, Range(0f, 1f)] public float RareAuraAlpha { get; private set; } = 0.55f;
		[field: SerializeField, Range(0f, 1f)] public float EpicAuraAlpha { get; private set; } = 0.75f;

		[field: Header("Chain")]
		[field: SerializeField, Tooltip("연속 레벨업이면 일괄 공개 길. 풀 연출은 끔 (처음 1회는 연속이어도 풀)")] public bool ChainPath { get; private set; }
		[field: SerializeField, Range(0.1f, 1f), Tooltip("일괄 공개 팩 높이, 무대 높이 대비")] public float ChainPackHeightRatio { get; private set; } = 0.32f;
		[field: SerializeField, Min(0f)] public float ChainEnter { get; private set; } = 0.12f;
		[field: SerializeField, Min(0.01f)] public float ChainBurstAt { get; private set; } = 0.15f;
		[field: SerializeField, Range(0f, 1f), Tooltip("일괄 공개 폭발 크기 비율")] public float ChainBurstRatio { get; private set; } = 0.7f;
		[field: SerializeField, Min(0.01f)] public float ChainFly { get; private set; } = 0.22f;

		public override Sprite BackSprite(CardRevealTier tier) => TierArt(Backs, tier);

		private static T TierArt<T>(T[] variants, CardRevealTier tier) where T : UnityEngine.Object
		{
			return variants == null || variants.Length == 0 ? null : variants[Mathf.Clamp((int)tier, 0, variants.Length - 1)];
		}

		private float BackEffect(CardRevealRun run, CardRevealCard card, float at)
		{
			Texture2D texture = TierArt(BackEffects, card.Tier);
			if (texture == null)
				return 0f;
			float seconds = BackEffectSeconds[(int)card.Tier];
			if (IsChain(run))
				seconds *= 0.75f;
			if (seconds > 0f)
				run.Play(at, card.BackEffect(texture, seconds));
			return Mathf.Max(0f, seconds);
		}

		private bool IsChain(CardRevealRun run) => ChainPath && run.Chained;
		private float PackHeight(CardRevealRun run) => run.Stage.Size.y * (IsChain(run) ? ChainPackHeightRatio : PackHeightRatio);
		private float TierValue(Vector3 values, CardRevealTier tier) => tier >= CardRevealTier.Epic ? values.z : tier == CardRevealTier.Rare ? values.y : values.x;

		public override void Build(CardRevealRun run)
		{
			Color top = run.BestColor;
			bool chain = IsChain(run);
			float packHeight = PackHeight(run);
			RevealSource source = run.ChestSprite != null
				? RevealSource.Chest(run, run.ChestSprite, ChestSize)
				: RevealSource.PackArt(run, TierArt(PackArt, run.Best), packHeight, TearRatio, top, TierArt(PackBands, run.Best));
			float burstAt = chain ? ChainBurstAt : TierValue(BurstAt, run.Best);
			Vector2 home = source.Root.anchoredPosition;

			// 1. 등장
			float enter = chain ? ChainEnter : Enter;
			source.Root.localScale = Vector3.one * EnterStartScale;
			if (chain == false)
			{
				source.Root.anchoredPosition = home + new Vector2(0f, run.Stage.Size.y * EnterDrop);
				source.Root.localRotation = Quaternion.Euler(0f, 0f, EnterTilt);
				run.Play(0f, source.Root.DOAnchorPos(home, enter).SetEase(Ease.OutBack));
				run.Play(0f, source.Root.DOLocalRotateQuaternion(Quaternion.identity, enter).SetEase(Ease.OutBack));
			}
			run.Play(0f, source.Root.DOScale(1f, enter).SetEase(Ease.OutBack));
			run.Sfx(0f, WhooshSfx);

			// 2. 등급 예고
			float teaseAt = chain ? 0f : TeaseAt;
			BuildTease(run, source, top, teaseAt, burstAt, packHeight, chain);

			// 3. 밀어 들어가기와 떨림
			if (chain == false)
				BuildPush(run, source, home, burstAt);

			// 4. 찢김과 폭발
			BuildBurst(run, source, top, burstAt, packHeight, chain ? ChainBurstRatio : 1f);

			// 5. 카드 날아감
			Vector2 mouth = run.Origin + new Vector2(0f, packHeight * MouthRatio);
			float fly = chain ? ChainFly : Fly;
			for (int i = 0; i < run.Cards.Count; i++)
				FlyIn(run, run.Cards[i], i, burstAt + i * FlyStagger, mouth, fly);
			float landed = burstAt + fly + FlyStagger * (run.Cards.Count - 1);
			foreach (CardRevealCard card in run.Cards)
				if (card.Tier != CardRevealTier.Common)
					run.Play(landed, card.Aura.DOFade(card.Tier >= CardRevealTier.Epic ? EpicAuraAlpha : RareAuraAlpha, AuraIn));

			// 6. 뒤집기
			BuildFlips(run, landed);
		}

		private void BuildTease(CardRevealRun run, RevealSource source, Color top, float teaseAt, float burstAt, float packHeight, bool chain)
		{
			float teaseIn = chain ? Mathf.Min(TeaseIn, burstAt) : TeaseIn;
			Image glow = run.Stage.NewImage(false, RevealTextures.Bloom, new Color(top.r, top.g, top.b, 0f), run.Origin, Vector2.one * packHeight * TeaseRadiusRatio * 2f, "[Reveal] Tease");
			glow.transform.SetSiblingIndex(source.Root.GetSiblingIndex());
			run.Play(teaseAt, glow.DOFade(TeaseAlpha, teaseIn).SetEase(Ease.OutQuad));
			run.Play(burstAt, glow.DOFade(0f, SourceOut));
			if (chain)
				return;

			int rays = run.Best >= CardRevealTier.Epic ? EpicRays : run.Best == CardRevealTier.Rare ? RareRays : 0;
			if (rays > 0)
			{
				Image holder = run.Stage.NewImage(false, null, Color.clear, run.Origin, Vector2.one, "[Reveal] Rays");
				holder.transform.SetSiblingIndex(glow.transform.GetSiblingIndex());
				CanvasGroup rayGroup = holder.gameObject.AddComponent<CanvasGroup>();
				rayGroup.alpha = 0f;
				for (int i = 0; i < rays; i++)
				{
					GameObject rayObject = new("[Image] Ray", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
					RectTransform ray = (RectTransform)rayObject.transform;
					ray.SetParent(holder.rectTransform, false);
					ray.pivot = new Vector2(0.5f, 0f);
					ray.sizeDelta = new Vector2(packHeight * RayWidthRatio, packHeight * RayLengthRatio);
					ray.localRotation = Quaternion.Euler(0f, 0f, 360f * i / rays);
					Image rayImage = rayObject.GetComponent<Image>();
					rayImage.sprite = RevealTextures.Beam;
					rayImage.color = new Color(top.r, top.g, top.b, RayAlpha);
					rayImage.raycastTarget = false;
				}
				float spin = burstAt - teaseAt;
				run.Play(teaseAt, rayGroup.DOFade(1f, teaseIn));
				run.Play(teaseAt, DOVirtual.Float(0f, RaySpeed * spin, spin, angle => holder.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle)).SetEase(Ease.InQuad));
				run.Play(burstAt, rayGroup.DOFade(0f, SourceOut));
			}

			if (PulseGap > 0f)
				for (float pulse = teaseAt + teaseIn * 0.5f; pulse < burstAt - 0.1f; pulse += PulseGap)
					run.Ring(pulse, run.Origin, packHeight * 0.9f, 1.5f, top, PulseGap * 1.4f);
			if (MoteGap > 0f)
				for (float wave = teaseAt; wave < burstAt; wave += MoteGap)
					run.Emit(wave, () => EmitMotes(run, source.Size, top));

			if (SheenTime > 0f && source.Body != null)
				BuildSheen(run, source, SheenAt);
		}

		private void BuildPush(CardRevealRun run, RevealSource source, Vector2 home, float burstAt)
		{
			float pushAt = Mathf.Min(PushAt, burstAt - 0.05f);
			float push = burstAt - pushAt;
			run.Play(pushAt, DOVirtual.Float(0f, 1f, push, progress =>
			{
				float strength = progress * progress * TrembleMax;
				float phase = progress * push * TrembleSpeed;
				source.Root.anchoredPosition = home + new Vector2(Mathf.Sin(phase * 2.7f), Mathf.Cos(phase * 3.1f) * 0.5f) * strength;
				source.Root.localRotation = Quaternion.Euler(0f, 0f, TrembleMax > 0f ? Mathf.Sin(phase * 1.9f) * strength * TrembleAngle / TrembleMax : 0f);
				source.Root.localScale = Vector3.one * Mathf.Lerp(1f, PushScale, progress);
			}).SetEase(Ease.InQuad));
			run.Play(pushAt, run.Stage.Dim.DOFade(PushDim, push).SetEase(Ease.InQuad));
			if (PushCameraDistance < 1f)
				run.CameraPunch(pushAt, PushCameraDistance, 0f, push);
			for (int tick = 0; tick < TrembleTicks; tick++)
				run.Sfx(pushAt + push * Mathf.Sqrt((float)tick / TrembleTicks), TickSfx);

			if (source.Seam != null && source.Body != null)
			{
				float seamAt = Mathf.Min(SeamAt, burstAt - 0.1f);
				float seam = burstAt - seamAt;
				Image seamImage = source.Seam;
				run.Play(seamAt, seamImage.DOFade(1f, seam).SetEase(Ease.InQuad));
				run.Play(seamAt, seamImage.rectTransform.DOScaleY(2.2f, seam).SetEase(Ease.InQuad));
				run.Play(burstAt, seamImage.DOFade(0f, SourceOut));
			}
		}

		private void BuildBurst(CardRevealRun run, RevealSource source, Color top, float burstAt, float packHeight, float scale)
		{
			CardRevealTier best = run.Best;
			bool commonOnly = best == CardRevealTier.Common;
			float stageHeight = run.Stage.Size.y;
			source.Open(run, burstAt);
			run.Play(burstAt, source.Group.DOFade(0f, SourceOut));
			run.Play(burstAt, source.Root.DOScale(SourceOutScale, SourceOut).SetEase(Ease.OutQuad));
			run.Play(burstAt, run.Stage.Dim.DOFade(DimAlpha, BloomTime));
			float radius = stageHeight * (commonOnly ? CommonBloomRadiusRatio : BloomRadiusRatio) * scale;
			run.Bloom(burstAt, run.Origin, radius, top, BloomPeak, BloomTime);
			run.Bloom(burstAt, run.Origin, stageHeight * WhiteRadiusRatio * scale, Color.white, WhitePeak, BloomTime * 0.8f);
			if (commonOnly == false)
				run.Ring(burstAt, run.Origin, packHeight, RingScale, top, RingTime);
			run.Shake(burstAt, TierValue(BurstShake, best) * scale, BurstShakeTime);
			run.Sfx(burstAt, BurstSfx);
			RevealBurst sparks = BurstSparks;
			sparks.Count = Mathf.RoundToInt(TierValue(SparkCount, best) * scale);
			run.Burst(burstAt, run.Origin, sparks, top);
			run.Burst(burstAt, run.Origin, BurstGlow, Color.white);
			if (commonOnly == false && PillarTime > 0f)
				Pillar(run, burstAt, source.Size.x * PillarWidthRatio, top);
			if (KickCameraDistance < 1f && IsChain(run) == false)
				run.At(burstAt, () =>
				{
					if (run.Skipping == false && CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
						run.AddRelease(cameraManager.PunchZoom(KickCameraDistance, 0f, 0.06f, KickHold, CameraOut, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
				});
		}

		// 팩 입구에서 위로 솟는 빛기둥. 위로 뻗은 뒤 폭이 줄며 사라짐
		private void Pillar(CardRevealRun run, float at, float width, Color tint)
		{
			Vector2 bottom = run.Origin;
			float height = run.Stage.Size.y * 0.5f - bottom.y + 40f;
			Image beam = run.Stage.NewImage(false, RevealTextures.Beam, new Color(tint.r, tint.g, tint.b, 0f), bottom, new Vector2(width, height), "[Reveal] Pillar");
			beam.rectTransform.pivot = new Vector2(0.5f, 0f);
			beam.rectTransform.localScale = new Vector3(1f, 0f, 1f);
			run.Play(at, beam.DOFade(1f, PillarTime * 0.15f));
			run.Play(at, beam.rectTransform.DOScaleY(1f, PillarTime * 0.25f).SetEase(Ease.OutQuad));
			run.Play(at + PillarTime * 0.3f, beam.rectTransform.DOScaleX(0.15f, PillarTime * 0.7f).SetEase(Ease.InQuad));
			run.Play(at + PillarTime * 0.3f, beam.DOFade(0f, PillarTime * 0.7f).SetEase(Ease.InQuad));
		}

		// 팩 몸통 위를 비스듬한 빛 띠가 한 번 훑음
		private void BuildSheen(CardRevealRun run, RevealSource source, float at)
		{
			Vector2 size = source.Size;
			GameObject maskObject = new("[Reveal] Sheen", typeof(RectTransform), typeof(RectMask2D));
			RectTransform mask = (RectTransform)maskObject.transform;
			mask.SetParent(source.Root, false);
			mask.sizeDelta = new Vector2(size.x * 0.9f, size.y * (1f - TearRatio * 2f));
			mask.anchoredPosition = new Vector2(0f, -size.y * TearRatio * 0.5f);
			GameObject bandObject = new("[Image] Band", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			RectTransform band = (RectTransform)bandObject.transform;
			band.SetParent(mask, false);
			band.sizeDelta = new Vector2(size.x * 0.45f, size.y * 1.6f);
			band.localRotation = Quaternion.Euler(0f, 0f, -ShineAngle);
			band.anchoredPosition = new Vector2(-size.x, 0f);
			Image bandImage = bandObject.GetComponent<Image>();
			bandImage.sprite = RevealTextures.Beam;
			bandImage.color = new Color(1f, 1f, 1f, SheenAlpha);
			bandImage.raycastTarget = false;
			run.Play(at, band.DOAnchorPosX(size.x, SheenTime).SetEase(Ease.InOutSine));
		}

		private void EmitMotes(CardRevealRun run, Vector2 sourceSize, Color tint)
		{
			UIRevealParticles particles = run.Stage.BackParticles;
			for (int index = 0; index < MotesPerWave; index++)
				particles.Add(new UIRevealParticles.Particle
				{
					Position = run.Origin + new Vector2(Random.Range(-0.6f, 0.6f) * sourceSize.x, Random.Range(-0.55f, -0.2f) * sourceSize.y),
					Velocity = new Vector2(Random.Range(-20f, 20f), Random.Range(MoteSpeed.x, MoteSpeed.y)),
					Drag = 0.4f,
					Life = MoteLife,
					Size = MoteSize,
					Tint = tint,
					Kind = index % 2 == 0 ? RevealParticleKind.Twinkle : RevealParticleKind.Dot,
				});
		}

		private void FlyIn(CardRevealRun run, CardRevealCard card, int index, float at, Vector2 mouth, float fly)
		{
			RectTransform rect = card.Rect;
			Vector3 from = run.Stage.ToParentLocal(mouth, rect.parent);
			float tilt = (index - (run.Cards.Count - 1) * 0.5f + Random.Range(-0.3f, 0.3f)) * FlyTilt;
			rect.localPosition = from;
			rect.localScale = card.HomeScale * FlyStartScale;
			rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
			run.Play(at, card.Group.DOFade(1f, Mathf.Min(0.08f, fly)));
			run.Play(at, rect.DOLocalMove(card.HomePosition, fly).SetEase(Ease.OutCubic));
			run.Play(at, rect.DOScale(card.HomeScale, fly).SetEase(Ease.OutBack));
			run.Play(at, rect.DOLocalRotateQuaternion(card.HomeRotation, fly).SetEase(Ease.OutCubic));
			run.Sfx(at + fly, LandSfx);
		}
	}
}
