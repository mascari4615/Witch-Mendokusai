using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화염방사 강한 판 (강력 스킬용). 주변 불씨가 손으로 빨려든 뒤 거대한 불길이 앞으로 뿜어져 1.6초 동안 땅을 태우고, 땅에서 불기둥이 솟고, 끝에서 연기 기둥이 말려 오름
	/// - 흐름: 역류 모음 (불씨가 손으로, 손 불덩이와 점광이 커짐) -> 방출 (원뿔로 넓어지는 불길 입자, 뼈대 원뿔, 불길 점광이 끝을 따라감) -> 땅 불기둥 넷 -> 끊김과 연기 버섯 -> 잔불
	/// - 방향은 시전자 조준. 진열장은 화면 왼쪽 (aimSide) 으로, 오른쪽 위 HUD 와 작은 지도에 불길 끝이 가리지 않게
	/// - 흔들림, 정지, 섬광은 화면 연출 박자표 (FeelUltimateFx) 몫. 여기는 월드 안 불, 연기, 빛, 그을림
	/// - 설계 memo wm/design/ult3d-all-20261120/01_flame.md, 제작 기록 ult3d-batch2-20261121
	/// - 재생: 개발 창 fx ultimate FlameHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_FlameHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(FlameHeavyUltimateFx))]
	public sealed class FlameHeavyUltimateFx : WorldUltimateFx
	{
		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color fireTint = new(1.2f, 0.52f, 0.18f);
		[SerializeField, ColorUsage(false, true)] private Color coreTint = new(1.5f, 0.85f, 0.3f);
		[SerializeField] private Color fireLightColor = new(1f, 0.55f, 0.2f, 1f);
		[SerializeField] private Color smokeColor = new(0.17f, 0.15f, 0.14f, 0.95f);
		[SerializeField] private Color emberColor = new(1f, 0.6f, 0.2f, 1f);
		[SerializeField] private Color scorchColor = new(0.06f, 0.04f, 0.03f, 0.85f);
		[SerializeField] private Color scorchLine = new(1f, 0.45f, 0.1f, 1f);

		[Header("방향")]
		[SerializeField, Range(-60f, 60f), Tooltip("화면 옆 기준 아래 (카메라 쪽) 로 꺾는 각도")] private float aimDown = 10f;
		[SerializeField, Tooltip("1 이면 화면 오른쪽, -1 이면 왼쪽. 오른쪽은 작은 지도 HUD 에 끝이 가림 (독립 검토 4회차)")] private float aimSide = -1f;
		[SerializeField, Min(0f), Tooltip("불길 시작점을 손 앞으로 빼는 거리 (인물을 가리지 않게)")] private float muzzleForward = 0.8f;
		[SerializeField, Min(0f)] private float muzzleHeight = 0.9f;

		[Header("불길")]
		[SerializeField, Min(1f), Tooltip("불길 길이 (인물 키 0.8 의 약 11배)")] private float flameLength = 4.8f;
		[SerializeField, Min(0.2f), Tooltip("끝 반지름")] private float flameEndRadius = 2.3f;
		[SerializeField, Min(0)] private int firePerSecond = 300;
		[SerializeField, Min(0)] private int corePerSecond = 50;
		[SerializeField, Min(0)] private int smokePerSecond = 34;
		[SerializeField] private Vector2 fireSize = new(0.7f, 1.1f);
		[SerializeField, Min(1f), Tooltip("수명 동안 커지는 배율")] private float fireGrowth = 3.4f;
		[SerializeField, Min(0.1f)] private float fireLifetime = 0.5f;
		[SerializeField, Range(0f, 1f), Tooltip("뼈대 원뿔 밝기 상한 (입자가 주, 원뿔은 빈틈만)")] private float coneBrightness = 0.35f;
		[SerializeField, Range(0.1f, 1f), Tooltip("불길 세로 퍼짐 / 가로 퍼짐. 쿼터뷰에서 위로 보는 폭은 가로, 세로는 땅을 뚫지 않게 납작하게")] private float verticalSpread = 0.35f;
		[SerializeField, Range(0f, 0.5f), Tooltip("불길 축을 위로 드는 비율 (끝이 땅에 박히지 않게)")] private float upwardBias = 0.06f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("역류 모음 (m01 0.8s)")] private float gatherSeconds = 0.9f;
		[SerializeField, Min(0.2f), Tooltip("불길 지속 (m01 거대 화염 1.5s)")] private float flameSeconds = 1.6f;
		[SerializeField, Min(0.1f), Tooltip("뼈대 원뿔이 다 뻗는 시간. 입자가 끝까지 가는 시간 (불길 수명) 과 맞춤")] private float reachSeconds = 0.45f;
		[SerializeField, Min(0.1f)] private float smokeSeconds = 2.4f;
		[SerializeField, Min(0.1f)] private float emberLinger = 1.6f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.2f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.55f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.65f;
		[SerializeField, Min(0f)] private float handIntensity = 10f;
		[SerializeField] private Vector2 handRange = new(3f, 6f);
		[SerializeField, Min(0f)] private float flameIntensity = 24f;
		[SerializeField, Min(1f)] private float flameRange = 12f;

		[Header("후처리")]
		[SerializeField, Min(0f)] private float bloom = 0.9f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.22f;
		[SerializeField, Range(0f, 60f)] private float fireSaturation = 15f;

		[Header("반응")]
		[SerializeField, Min(0)] private int gatherCount = 70;
		[SerializeField] private Vector2 gatherRadius = new(2.5f, 6f);
		[SerializeField, Min(0)] private int pillarCount = 4;
		[SerializeField, Min(0)] private int pillarPuffs = 34;
		[SerializeField, Min(0.5f)] private float pillarHeight = 4.5f;
		[SerializeField, Min(0.05f)] private float pillarInterval = 0.22f;
		[SerializeField, Min(0)] private int mushroomPuffs = 56;
		[SerializeField, Min(0.5f)] private float mushroomHeight = 5.5f;
		[SerializeField, Min(0)] private int groundEmbers = 70;
		[SerializeField, Min(1f)] private float windSpeed = 12f;

		// ScaleWorld 배율. 실행용 복제본에서만 1 이 아님 (리터럴 크기 값에 곱함)
		[System.NonSerialized] private float sizeScale = 1f;

		private float ReleaseTime => gatherSeconds;
		private float FlameEnd => ReleaseTime + flameSeconds;
		private float RestoreTime => FlameEnd + smokeSeconds * 0.5f;

		public override float DurationSeconds => FlameEnd + Mathf.Max(smokeSeconds, emberLinger);

		public override void ScaleWorld(float scale)
		{
			flameLength *= scale;
			flameEndRadius *= scale;
			fireSize *= scale;
			flameRange *= scale;
			handRange *= scale;
			gatherRadius *= scale;
			pillarHeight *= Mathf.Pow(scale, 0.6f);
			mushroomHeight *= Mathf.Pow(scale, 0.5f);
			sizeScale = scale;
			firePerSecond = Mathf.RoundToInt(firePerSecond * scale);
			corePerSecond = Mathf.RoundToInt(corePerSecond * scale);
			smokePerSecond = Mathf.RoundToInt(smokePerSecond * scale);
			pillarPuffs = Mathf.RoundToInt(pillarPuffs * scale);
			mushroomPuffs = Mathf.RoundToInt(mushroomPuffs * scale);
			groundEmbers = Mathf.RoundToInt(groundEmbers * scale);
		}

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float angle = aimDown * Mathf.Deg2Rad;
			Vector3 direction = (run.ScreenRight * Mathf.Cos(angle) * Mathf.Sign(aimSide) - run.ScreenForward * Mathf.Sin(angle)).normalized;
			Vector3 muzzle = run.Caster + Vector3.up * muzzleHeight + direction * muzzleForward;
			Vector3 tip = muzzle + direction * flameLength;
			tip.y = UltimateFxStage.GroundHeight(tip);

			ParticleSystem fire = run.Particles("FlameFire", run.Additive(UltimateFxTextures.FlamePuff, fireTint), firePerSecond + 200, -0.25f);
			UltimateFxStage.SetLifetimeLook(fire, FireGradient(), AnimationCurve.EaseInOut(0f, 1f, 1f, fireGrowth));
			ParticleSystem.NoiseModule turbulence = fire.noise;
			turbulence.enabled = true;
			turbulence.strength = 1.4f;
			turbulence.frequency = 0.6f;

			ComposeGather(stage, muzzle);
			ComposeFlame(stage, muzzle, direction, tip, fire);
			ComposePillars(stage, muzzle, direction, fire);
			ComposeMushroom(stage, tip, fire);
			ComposeGrade(stage);
		}

		// 흰 노랑 -> 주황 -> 짙은 빨강 -> 사라짐
		private static Gradient FireGradient()
		{
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f), new GradientColorKey(new Color(0.45f, 0.1f, 0.03f), 0.7f), new GradientColorKey(Color.black, 1f) },
				new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.06f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
			return gradient;
		}

		// 역류 모음: 둘레 불씨가 손으로 빨려듦, 손 불덩이와 손 점광이 커짐
		private void ComposeGather(Stage stage, Vector3 muzzle)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem embers = Motes(run, "FlameGather", gatherCount + 10, 0f, 0.3f);
			for (int i = 0; i < gatherCount; i++)
			{
				float time = Mathf.Lerp(0f, ReleaseTime - 0.25f, i / (float)Mathf.Max(1, gatherCount - 1));
				run.At(time, () =>
				{
					Vector3 from = run.Caster + UltimateFxStage.RandomRingPoint(gatherRadius.x, gatherRadius.y) + Vector3.up * Random.Range(0.1f, 1.6f);
					float life = Random.Range(0.3f, 0.45f);
					UltimateFxStage.Emit(embers, from, (muzzle - from) / life, Random.Range(0.16f, 0.3f) * sizeScale, life, emberColor);
				});
			}

			// 손 불덩이: 불 입자가 손 둘레를 돌며 점점 크고 많아짐. 매끈한 구는 크림색 원판으로 읽힘 (독립 검토 4회차)
			ParticleSystem charge = run.Particles("FlameCharge", run.Additive(UltimateFxTextures.FlamePuff, fireTint), 220, -0.1f);
			UltimateFxStage.SetLifetimeLook(charge, FireGradient(), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));
			float chargeCarry = 0f;
			run.Track(0.1f, ReleaseTime - 0.1f, _ =>
			{
				float grow = UltimateFxRun.EaseIn(UltimateFxRun.Span(run.Now, 0f, ReleaseTime));
				chargeCarry += Mathf.Lerp(20f, 140f, grow) * run.Delta;
				for (; chargeCarry >= 1f; chargeCarry -= 1f)
				{
					Vector3 offset = Random.onUnitSphere * Mathf.Lerp(0.25f, 0.8f, grow) * sizeScale;
					Vector3 swirl = Vector3.Cross(Vector3.up, offset).normalized * 3f * sizeScale;
					UltimateFxStage.Emit(charge, muzzle + offset, swirl - offset * 2f, Random.Range(0.35f, 0.7f) * Mathf.Lerp(0.6f, 1.4f, grow) * sizeScale, Random.Range(0.25f, 0.4f), Color.white, Random.value * 360f, Random.Range(-200f, 200f));
				}
			});

			Light hand = WorldLight(stage, "FlameHand", LightType.Point, fireLightColor, handRange.y);
			hand.transform.position = muzzle + stage.ToCamera * 0.3f;
			run.Track(0f, ReleaseTime + 0.2f, _ =>
			{
				float grow = UltimateFxRun.EaseIn(UltimateFxRun.Span(run.Now, 0f, ReleaseTime));
				float fade = 1f - UltimateFxRun.Span(run.Now, ReleaseTime, ReleaseTime + 0.2f);
				hand.range = Mathf.Lerp(handRange.x, handRange.y, grow);
				hand.intensity = handIntensity * grow * fade * (1f + 0.15f * Mathf.Sin(run.Now * 29f));
			}, () => hand.enabled = false);
		}

		// 방출: 원뿔로 넓어지는 불길 입자 (주), 흰 심, 뼈대 원뿔 (보조), 연기, 불길 점광, 그을림 띠, 풀 바람
		private void ComposeFlame(Stage stage, Vector3 muzzle, Vector3 direction, Vector3 tip, ParticleSystem fire)
		{
			UltimateFxRun run = stage.Run;
			Material coreMaterial = run.Additive(UltimateFxTextures.FlamePuff, coreTint);
			ParticleSystem core = run.Particles("FlameCore", coreMaterial, corePerSecond + 60, 0f);
			UltimateFxStage.SetLifetimeLook(core, UltimateFxStage.FadeGradient(Color.white, 0.05f, 0.4f), AnimationCurve.EaseInOut(0f, 0.8f, 1f, 1.6f));
			ParticleSystem smoke = run.Particles("FlameSmoke", LitParticle(run, UltimateFxTextures.Smoke, 0.8f), smokePerSecond * 3 + 40, -0.12f);
			UltimateFxStage.SetLifetimeLook(smoke, UltimateFxStage.FadeGradient(Color.white, 0.15f, 0.55f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));

			float speed = flameLength / fireLifetime;
			float spread = Mathf.Atan2(flameEndRadius, flameLength);
			Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
			Vector3 up = Vector3.Cross(direction, side);
			Vector3 axis = (direction + Vector3.up * upwardBias).normalized;
			float fireCarry = 0f;
			float coreCarry = 0f;
			float smokeCarry = 0f;
			run.Track(ReleaseTime, flameSeconds, progress =>
			{
				float delta = run.Delta;
				// 끝 0.15초는 줄어들며 끊김
				float strength = 1f - UltimateFxRun.Span(run.Now, FlameEnd - 0.15f, FlameEnd) * 0.8f;
				fireCarry += firePerSecond * delta * strength;
				coreCarry += corePerSecond * delta * strength;
				smokeCarry += smokePerSecond * delta;
				for (; fireCarry >= 1f; fireCarry -= 1f)
				{
					Vector2 cone = Random.insideUnitCircle * Mathf.Tan(spread);
					Vector3 velocity = (axis + side * cone.x + up * cone.y * verticalSpread).normalized * speed * Random.Range(0.85f, 1.1f);
					UltimateFxStage.Emit(fire, muzzle + Random.insideUnitSphere * 0.15f, velocity, Random.Range(fireSize.x, fireSize.y), fireLifetime * Random.Range(0.85f, 1.15f), Color.white, Random.value * 360f, Random.Range(-180f, 180f));
				}
				for (; coreCarry >= 1f; coreCarry -= 1f)
				{
					Vector2 cone = Random.insideUnitCircle * Mathf.Tan(spread) * 0.35f;
					Vector3 velocity = (axis + side * cone.x + up * cone.y * verticalSpread).normalized * speed * Random.Range(0.6f, 0.8f);
					UltimateFxStage.Emit(core, muzzle, velocity, Random.Range(fireSize.x, fireSize.y) * 0.7f, fireLifetime * 0.6f, Color.white, Random.value * 360f);
				}
				for (; smokeCarry >= 1f; smokeCarry -= 1f)
				{
					float along = Random.Range(0.45f, 1f);
					Vector3 position = muzzle + direction * flameLength * along + side * Random.Range(-1f, 1f) * flameEndRadius * along + Vector3.up * Random.Range(0.6f, 1.6f);
					Color tone = smokeColor * Random.Range(0.8f, 1.15f);
					tone.a = smokeColor.a;
					UltimateFxStage.Emit(smoke, position, (Vector3.up * Random.Range(2.5f, 4.5f) + direction * Random.Range(1f, 3f)) * sizeScale, Random.Range(1.2f, 2f) * flameEndRadius * 0.6f, Random.Range(1f, 1.5f), tone, Random.value * 360f, Random.Range(-40f, 40f));
				}
			});

			// 뼈대 원뿔: 입자 사이 빈틈만. 반복 잡음 무늬를 길이 방향으로 흘리고 둘레로 밀어 도는 느낌 (메시는 돌리지 않음, 납작한 단면 유지)
			Mesh coneMesh = Keep(run, UltimateMeshFactory.Cone(28, 14, 0.08f, 3f));
			Material coneMaterial = run.Additive(UltimateFxTextures.TiledStreaks, Color.clear);
			GameObject coneObject = UltimateFxStage.CreateMeshObject("FlameCone", run.Root, coneMesh, coneMaterial);
			coneObject.transform.position = muzzle;
			coneObject.SetActive(false);
			run.At(ReleaseTime, () => coneObject.SetActive(true));
			run.Track(ReleaseTime, flameSeconds, progress =>
			{
				float reach = UltimateFxRun.EaseOut(UltimateFxRun.Span(run.Now, ReleaseTime, ReleaseTime + reachSeconds));
				float cut = 1f - UltimateFxRun.Span(run.Now, FlameEnd - 0.15f, FlameEnd);
				coneObject.transform.rotation = Quaternion.LookRotation(axis);
				coneObject.transform.localScale = new Vector3(flameEndRadius * 0.9f, flameEndRadius * verticalSpread, flameLength * 0.95f * reach);
				coneMaterial.mainTextureOffset = new Vector2(run.Now * 0.25f, -run.Now * 2.6f);
				coneMaterial.SetColor("_Color", fireTint * coneBrightness * cut * (0.85f + 0.15f * Mathf.Sin(run.Now * 23f)));
			}, () => coneObject.SetActive(false));

			// 불길 점광: 시전자에서 끝 쪽으로 따라 이동, 끊긴 뒤 연기 버섯 아래로
			Light flame = WorldLight(stage, "FlameLight", LightType.Point, fireLightColor, flameRange);
			run.Track(ReleaseTime, DurationSeconds - ReleaseTime, _ =>
			{
				float reach = UltimateFxRun.EaseOut(UltimateFxRun.Span(run.Now, ReleaseTime, ReleaseTime + 0.5f));
				float toTip = UltimateFxRun.EaseInOut(UltimateFxRun.Span(run.Now, FlameEnd - 0.2f, FlameEnd + 0.2f));
				Vector3 along = Vector3.Lerp(muzzle + direction * flameLength * 0.6f * reach, tip + Vector3.up * 1.5f, toTip);
				flame.transform.position = along + Vector3.up * 0.8f + stage.ToCamera * 0.5f;
				float flicker = 1f + 0.12f * Mathf.Sin(run.Now * 31f) + 0.08f * Mathf.Sin(run.Now * 53f);
				float level = run.Now < FlameEnd ? 1f : Mathf.Lerp(0.6f, 0f, UltimateFxRun.Span(run.Now, FlameEnd, FlameEnd + smokeSeconds));
				flame.intensity = flameIntensity * reach * level * flicker;
			}, () => flame.enabled = false);
			GroundGlow(run, muzzle + direction * flameLength * 0.55f, flameLength * 1.3f, fireTint * 0.25f, ReleaseTime, 0.15f, flameSeconds * 0.6f, flameSeconds + 0.6f);

			// 그을림 띠: 불길 축을 따라 늘인 판, 불길이 지나가며 나타남. 잔불 점이 남아 깜빡이며 꺼짐
			Texture2D scorch = ScorchTexture(run, 57, scorchColor, scorchLine, 0.85f);
			Vector3 middle = muzzle + direction * flameLength * 0.55f;
			middle.y = UltimateFxStage.GroundHeight(middle);
			Material scorchMaterial = run.Alpha(scorch, new Color(1f, 1f, 1f, 0f));
			GameObject band = run.Plate("FlameScorch", scorchMaterial, middle + Vector3.up * 0.03f, 1f, 1);
			band.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z));
			band.transform.localScale = new Vector3(flameEndRadius * 1.6f, 1f, flameLength * 1.05f);
			run.Track(ReleaseTime + 0.1f, 0.5f, progress => scorchMaterial.SetColor("_Color", new Color(1f, 1f, 1f, UltimateFxRun.EaseOut(progress))));
			run.Track(DurationSeconds - 1.2f, 1.2f, progress => scorchMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 1f - progress)));

			ParticleSystem embers = Motes(run, "FlameGroundEmbers", groundEmbers + 10, 0f, 0f);
			UltimateFxStage.SetLifetimeLook(embers, EmberBlink(), null);
			for (int i = 0; i < groundEmbers; i++)
			{
				float along = Random.Range(0.15f, 1f);
				float time = ReleaseTime + along * 0.4f + Random.Range(0f, flameSeconds * 0.6f);
				run.At(time, () =>
				{
					Vector3 position = muzzle + direction * flameLength * along + side * Random.Range(-1f, 1f) * flameEndRadius * along;
					position.y = UltimateFxStage.GroundHeight(position) + 0.05f;
					UltimateFxStage.Emit(embers, position, Vector3.up * Random.Range(0f, 0.2f), Random.Range(0.06f, 0.14f), emberLinger * Random.Range(0.6f, 1f), emberColor);
				});
			}
			run.At(ReleaseTime, () => run.React(muzzle + direction * flameLength * 0.3f, windSpeed, flameLength, false));
		}

		// 깜빡이며 꺼지는 잔불
		private static Gradient EmberBlink()
		{
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.3f), 1f) },
				new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(0.4f, 0.3f), new GradientAlphaKey(0.9f, 0.45f), new GradientAlphaKey(0.3f, 0.7f), new GradientAlphaKey(0f, 1f) });
			return gradient;
		}

		// 땅 불기둥: 불길이 닿은 땅 몇 곳에서 차례로 불이 솟고 균열 그을림이 남음
		private void ComposePillars(Stage stage, Vector3 muzzle, Vector3 direction, ParticleSystem fire)
		{
			UltimateFxRun run = stage.Run;
			Texture2D crack = ScorchTexture(run, 58, scorchColor, scorchLine, 0.6f);
			Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
			for (int pillar = 0; pillar < pillarCount; pillar++)
			{
				float along = Mathf.Lerp(0.35f, 0.92f, pillar / (float)Mathf.Max(1, pillarCount - 1));
				Vector3 spot = muzzle + direction * flameLength * along + side * Random.Range(-0.5f, 0.5f) * flameEndRadius * along;
				spot.y = UltimateFxStage.GroundHeight(spot);
				float time = ReleaseTime + 0.3f + pillar * pillarInterval;
				run.At(time, () =>
				{
					for (int i = 0; i < pillarPuffs; i++)
					{
						Vector3 velocity = Vector3.up * pillarHeight / fireLifetime * Random.Range(0.6f, 1.1f) + Random.insideUnitSphere * 1.2f;
						UltimateFxStage.Emit(fire, spot + UltimateFxStage.RandomRingPoint(0f, 0.6f), velocity, Random.Range(fireSize.x, fireSize.y) * 0.9f, fireLifetime * Random.Range(0.9f, 1.3f), Color.white, Random.value * 360f, Random.Range(-120f, 120f));
					}
					run.SparkBurst(spot + Vector3.up * 0.3f, 24, new Vector2(4f, 9f), new Vector2(0.3f, 0.6f), new Vector2(0.06f, 0.12f), emberColor, Color.white, 1.2f);
				});
				GroundGlow(run, spot, 3.2f * sizeScale, fireTint * 0.6f, time, 0.03f, 0.3f, 1.4f);
				GroundDecal(run, crack, spot, 2.4f * sizeScale, time, 0.05f, DurationSeconds - 1f, 1f, 2);
			}
		}

		// 끊김과 연기 버섯: 끝에서 불이 위로 꺾여 솟고 회색 연기 기둥이 말려 오름 (아래에서 불길 점광이 비춤)
		private void ComposeMushroom(Stage stage, Vector3 tip, ParticleSystem fire)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem smoke = run.Particles("FlameMushroom", LitParticle(run, UltimateFxTextures.Smoke, 1f), mushroomPuffs + 10, -0.05f);
			UltimateFxStage.SetLifetimeLook(smoke, UltimateFxStage.FadeGradient(Color.white, 0.1f, 0.6f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.4f));
			ParticleSystem.NoiseModule swirl = smoke.noise;
			swirl.enabled = true;
			swirl.strength = 1.1f;
			swirl.frequency = 0.5f;
			run.At(FlameEnd - 0.2f, () =>
			{
				for (int i = 0; i < mushroomPuffs; i++)
				{
					float rise = Random.Range(0.3f, 1f);
					Color tone = smokeColor * Random.Range(0.75f, 1.1f);
					tone.a = smokeColor.a;
					Vector3 velocity = Vector3.up * mushroomHeight / smokeSeconds * rise * 1.6f + UltimateFxStage.RandomRingPoint(0f, 1f) * (rise * rise * 2.5f);
					UltimateFxStage.Emit(smoke, tip + Vector3.up * 0.5f + UltimateFxStage.RandomRingPoint(0f, 0.8f * sizeScale), velocity, Random.Range(1.6f, 2.6f) * sizeScale, smokeSeconds * Random.Range(0.8f, 1.1f), tone, Random.value * 360f, Random.Range(-50f, 50f));
				}
				for (int i = 0; i < mushroomPuffs; i++)
				{
					Vector3 velocity = Vector3.up * Random.Range(7f, 12f) + Random.insideUnitSphere * 2f;
					UltimateFxStage.Emit(fire, tip + Vector3.up * 0.3f + UltimateFxStage.RandomRingPoint(0f, 0.7f), velocity, Random.Range(fireSize.x, fireSize.y), fireLifetime * Random.Range(0.8f, 1.2f), Color.white, Random.value * 360f, Random.Range(-150f, 150f));
				}
				DustRing(run, tip, 26, 2.6f * sizeScale, smokeColor, 1.6f * sizeScale);
				run.React(tip, windSpeed, 6f * sizeScale, true);
			});
			GroundGlow(run, tip, 6f * sizeScale, fireTint * 0.8f, FlameEnd - 0.2f, 0.05f, 0.4f, 1.6f);
		}

		private void ComposeGrade(Stage stage)
		{
			float restore = RestoreTime;
			stage.BloomTint = new Color(1f, 0.7f, 0.4f);
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Plateau(now, 0f, gatherSeconds, restore, restoreSeconds));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Plateau(now, 0f, gatherSeconds, restore, restoreSeconds));
			stage.Vignette = now => vignette * Plateau(now, 0f, gatherSeconds, restore, restoreSeconds);
			stage.Saturation = now => fireSaturation * Plateau(now, ReleaseTime, 0.2f, restore, restoreSeconds);
			stage.Filter = now => Color.Lerp(Color.white, new Color(1f, 0.92f, 0.82f), Plateau(now, ReleaseTime, 0.2f, restore, restoreSeconds));
			stage.Bloom = now => Mathf.Max(0.4f * UltimateFxRun.Span(now, 0f, gatherSeconds), bloom * Plateau(now, ReleaseTime, 0.1f, FlameEnd, restoreSeconds)) * (1f - UltimateFxRun.Span(now, restore, restore + restoreSeconds));
		}
	}
}
