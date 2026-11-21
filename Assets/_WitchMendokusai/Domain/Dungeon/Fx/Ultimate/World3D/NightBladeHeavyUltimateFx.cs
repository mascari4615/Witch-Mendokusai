using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 밤의 날 강한 판 (강력 스킬용). 날 속에 밤하늘이 비치는 거대한 칼이 허공을 베면 벤 자리가 찢어져 그 너머 밤이 번지고, 별빛을 빨아들여 희게 달아오른 날이 다시 내리쳐 틈을 닫음
	/// - 흐름: 칼 나타남과 당김 -> 첫 베기 (빠른 단타) -> 검은 사선 -> 틈 열림과 번지는 밤 -> 별빛 흡수 (날이 희게) -> 둘째 베기 (땅을 그음, 틈 닫힘, 낮 복귀) -> 잔류
	/// - 칼과 틈은 같은 재질 (별밭 창, UltimateStarWindow). 별이 시선 방향으로 정해져 메시가 움직이면 안쪽 별이 따로 움직임. 틈은 화면 덧칠이 아니라 휘두름 면에 선 월드 판
	/// - 휘두름 면은 화면 오른쪽 축과 뒤로 기운 위 축이 만드는 평면. 칼은 이 면 위에서 시곗바늘처럼 돎 (각도 0 이 위, 양수가 aimSide 쪽)
	/// - 연출 광원은 칼날 점광과 틈 점광 둘. 흔들림, 정지, 섬광은 화면 연출 박자표 (FeelUltimateFx) 몫
	/// - 설계 memo wm/design/ult3d-all-20261120/08_night-blade.md, 제작 기록 ult3d-batch3-20261121
	/// - 재생: 개발 창 fx ultimate NightBladeHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_NightBladeHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(NightBladeHeavyUltimateFx))]
	public sealed partial class NightBladeHeavyUltimateFx : WorldUltimateFx
	{
		private static readonly int WHITE_ID = Shader.PropertyToID("_White");
		private static readonly int ALPHA_ID = Shader.PropertyToID("_Alpha");
		private static readonly int BRIGHTNESS_ID = Shader.PropertyToID("_Brightness");
		private static readonly int RIM_AMOUNT_ID = Shader.PropertyToID("_RimAmount");
		private static readonly int EDGE_AMOUNT_ID = Shader.PropertyToID("_EdgeAmount");

		/// <summary> 휘두름 면. 한 번의 연출 동안 고정 (시전 순간 자리) </summary>
		private sealed class Frame
		{
			public Vector3 Pivot;
			public Vector3 Side;
			public Vector3 PlaneUp;
			public Vector3 Normal;
			public float Length;

			/// <summary> 면 위 각도 (도) 의 방향. 0 이 위, 양수가 Side 쪽 </summary>
			public Vector3 Aim(float degrees) => PlaneUp * Mathf.Cos(degrees * Mathf.Deg2Rad) + Side * Mathf.Sin(degrees * Mathf.Deg2Rad);

			public Quaternion Pose(float degrees) => Quaternion.LookRotation(Normal, Aim(degrees));
		}

		[Header("3D 연결 (밤의 날)")]
		[SerializeField] private Shader starWindowShader;

		[Header("색")]
		[SerializeField] private Color spaceColor = new(0.03f, 0.02f, 0.09f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color nebulaColor = new(0.5f, 0.25f, 1.1f);
		[SerializeField, ColorUsage(false, true)] private Color starColor = new(2.4f, 2.6f, 3.4f);
		[SerializeField, ColorUsage(false, true)] private Color rimColor = new(1.6f, 0.9f, 3f);
		[SerializeField, ColorUsage(false, true)] private Color whiteColor = new(3.2f, 3.2f, 3.8f);
		[SerializeField] private Color bladeLightColor = new(0.7f, 0.55f, 1f, 1f);
		[SerializeField] private Color riftLightColor = new(0.5f, 0.55f, 1f, 1f);
		[SerializeField] private Color nightTint = new(0.72f, 0.66f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color scarColor = new(2.2f, 1.6f, 3.2f);
		[SerializeField] private Color dustColor = new(0.36f, 0.34f, 0.44f, 0.8f);

		[Header("칼")]
		[SerializeField, Min(1f), Tooltip("칼날 길이. 인물 키의 몇 배")] private float bladeLength = 4.6f;
		[SerializeField, Range(0.02f, 0.3f), Tooltip("칼날 폭 / 길이")] private float bladeWidth = 0.09f;
		[SerializeField, Range(0f, 0.3f), Tooltip("칼날이 등 쪽으로 휘는 정도 / 길이")] private float bladeCurve = 0.06f;
		[SerializeField, Min(0.2f), Tooltip("손 높이 (시전자 발 기준)")] private float pivotHeight = 1.1f;
		[SerializeField, Range(0f, 60f), Tooltip("휘두름 면을 뒤로 눕히는 각도. 0 이면 수직면, 크면 내려다보는 카메라 정면")] private float planeTilt = 30f;
		[SerializeField, Tooltip("1 이면 화면 오른쪽으로 벰, -1 이면 왼쪽. 오른쪽은 작은 지도 HUD 밑")] private float aimSide = -1f;
		[SerializeField, Tooltip("첫 베기 각도 (시작, 끝). 0 이 위, 양수가 베는 쪽")] private Vector2 firstSwing = new(-70f, 105f);
		[SerializeField, Range(0f, 40f), Tooltip("베기 전 뒤로 당기는 각도")] private float windBack = 14f;
		[SerializeField, Range(-90f, 90f), Tooltip("별빛을 모으는 동안 칼을 치켜든 각도")] private float raiseAngle = -22f;
		[SerializeField, Range(0f, 4f), Tooltip("칼날 테두리 (프레넬) 세기. 낮으면 멀리서 검은 판으로 읽힘")] private float bladeRim = 1.6f;
		[SerializeField, Tooltip("휘두름 자취 세기 (첫 베기 별밭 투명도, 둘째 베기 흰 빛). 높으면 자취가 흰 원판으로 읽힘 (1회차 촬영)")] private Vector2 trailBrightness = new(0.14f, 0.18f);
		[SerializeField, Min(1f), Tooltip("별 촘촘함. 작을수록 별이 크게 보임 (쿼터뷰 거리)")] private float starScale = 9f;
		[SerializeField, Range(0f, 1f)] private float starAmount = 0.6f;

		[Header("틈")]
		[SerializeField, Range(-60f, 90f), Tooltip("틈이 놓이는 각도 (휘두름 면 위). 틈은 이 방향에 직각으로 누움")] private float riftAngle = 46f;
		[SerializeField, Range(0.2f, 1f), Tooltip("손에서 틈 가운데까지 / 칼날 길이")] private float riftReach = 0.52f;
		[SerializeField, Min(1f)] private float riftLength = 7f;
		[SerializeField, Min(0.1f), Tooltip("다 열린 폭")] private float riftWidth = 2.1f;
		[SerializeField, Min(0.01f), Tooltip("열리기 전 검은 사선의 폭")] private float riftSlitWidth = 0.14f;
		[SerializeField, Range(0f, 6f), Tooltip("틈 가장자리 빛 세기. 높으면 안쪽 밤하늘이 묻혀 보라 잎사귀로 읽힘 (2회차 촬영)")] private float riftEdge = 1.3f;
		[SerializeField, Range(1f, 12f), Tooltip("틈 가장자리 빛의 폭. 클수록 가는 테두리")] private float riftEdgePower = 8f;
		[SerializeField, Range(0f, 0.4f), Tooltip("틈 윤곽의 들쭉날쭉함")] private float riftJagged = 0.08f;
		[SerializeField, Min(1f), Tooltip("틈 아래에서 번지는 밤의 반지름")] private float nightRadius = 8f;
		[SerializeField, Range(0f, 1f), Tooltip("번지는 밤 (바닥의 어두운 원) 진하기")] private float nightDarkness = 0.9f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("칼이 나타나 뒤로 당기는 시간")] private float windupSeconds = 0.42f;
		[SerializeField, Min(0.04f), Tooltip("첫 베기 (m08 휘두름 0.4s 를 단타로)")] private float slashSeconds = 0.16f;
		[SerializeField, Min(0.05f), Tooltip("검은 사선으로 머무는 시간 (m08 0.4s)")] private float slitSeconds = 0.4f;
		[SerializeField, Min(0.05f), Tooltip("틈이 열리는 시간 (m08 0.4s)")] private float openSeconds = 0.4f;
		[SerializeField, Min(0.5f), Tooltip("밤 (별빛 흡수와 흰 날 포함). 어두운 구간은 열림 + 이 값")] private float nightSeconds = 1.4f;
		[SerializeField, Min(0.04f), Tooltip("둘째 베기")] private float chopSeconds = 0.14f;
		[SerializeField, Min(0.1f)] private float lingerSeconds = 1.7f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 0.5f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.22f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.38f;
		[SerializeField, Tooltip("칼날 점광 세기 (별밭일 때, 흰 날일 때)")] private Vector2 bladeLightIntensity = new(8f, 24f);
		[SerializeField, Min(0f)] private float chopLightIntensity = 40f;
		[SerializeField, Min(1f)] private float bladeLightRange = 10f;
		[SerializeField, Min(0f)] private float riftLightIntensity = 12f;
		[SerializeField, Min(1f)] private float riftLightRange = 14f;

		[Header("후처리")]
		[SerializeField, Min(0f)] private float bloom = 1.8f;
		[SerializeField, Range(-100f, 0f), Tooltip("첫 베기 직후 0.2초 탈색 (m08)")] private float slashSaturation = -60f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.35f;

		[Header("반응")]
		[SerializeField, Min(0), Tooltip("틈 가장자리에서 떨어지는 빛 알갱이 (초당)")] private int edgeMotesPerSecond = 70;
		[SerializeField, Min(0), Tooltip("칼날로 빨려드는 별 입자 총수 (m08 400)")] private int absorbCount = 400;
		[SerializeField, Min(0.05f), Tooltip("땅에 남는 벤 자국 점 간격")] private float scarSpacing = 0.22f;
		[SerializeField, Min(0.2f)] private float scarWidth = 1.1f;
		[SerializeField, Min(0.1f), Tooltip("벤 자국이 빛나며 남는 시간")] private float scarSeconds = 1.5f;
		[SerializeField, Min(1f)] private float windSpeed = 14f;
		[SerializeField, Min(1f)] private float windReach = 10f;

		// ScaleWorld 배율. 실행용 복제본에서만 1 이 아님 (리터럴 크기 값에 곱함)
		[System.NonSerialized] private float sizeScale = 1f;

		private float SlashEnd => windupSeconds + slashSeconds;
		private float SlashTime => windupSeconds + slashSeconds * 0.5f;
		private float OpenStart => SlashTime + slitSeconds;
		private float OpenEnd => OpenStart + openSeconds;
		private float ChopTime => OpenEnd + nightSeconds;
		private float ChopStart => ChopTime - chopSeconds;
		private float AbsorbStart => OpenEnd + nightSeconds * 0.12f;
		private float AbsorbEnd => ChopStart - nightSeconds * 0.2f;

		public override float DurationSeconds => ChopTime + lingerSeconds;

		public override void ScaleWorld(float scale)
		{
			sizeScale = scale;
			// 칼은 덜 키움 (칼끝이 시야 위쪽 7.5 를 넘지 않게)
			bladeLength *= Mathf.Pow(scale, 0.75f);
			riftLength *= scale;
			riftWidth *= scale;
			riftSlitWidth *= scale;
			nightRadius *= scale;
			bladeLightRange *= scale;
			riftLightRange *= scale;
			scarWidth *= scale;
			windReach *= scale;
			absorbCount = Mathf.RoundToInt(absorbCount * scale);
		}

		protected override void ComposeWorld(Stage stage)
		{
			if (starWindowShader == null)
				throw new System.InvalidOperationException($"{name}: 별밭 창 셰이더 미연결. 메뉴 WM/CombatFx/Build Ultimate 3D Batch Presets");
			UltimateFxRun run = stage.Run;
			float side = Mathf.Sign(aimSide);
			Vector3 planeUp = (Vector3.up * Mathf.Cos(planeTilt * Mathf.Deg2Rad) + run.ScreenForward * Mathf.Sin(planeTilt * Mathf.Deg2Rad)).normalized;
			Vector3 origin = run.Caster;
			origin.y = UltimateFxStage.GroundHeight(origin);
			Frame frame = new()
			{
				Pivot = origin + Vector3.up * pivotHeight,
				Side = run.ScreenRight * side,
				PlaneUp = planeUp,
				// 날 (+X) 이 베는 쪽을 향하게
				Normal = Vector3.Cross(run.ScreenRight, planeUp) * side,
				Length = bladeLength,
			};
			Light bladeLight = WorldLight(stage, "NightBladeLight", LightType.Point, bladeLightColor, bladeLightRange);
			Light riftLight = WorldLight(stage, "NightRiftLight", LightType.Point, riftLightColor, riftLightRange);
			ComposeSword(stage, frame, bladeLight);
			ComposeRift(stage, frame, riftLight);
			ComposeAbsorb(stage, frame);
			ComposeChop(stage, frame, origin);
			ComposeGrade(stage);
		}

		/// <summary> 둘째 베기가 땅에 닿는 각도. 칼끝 높이가 땅이 되는 곳 </summary>
		private float GroundAngle(Frame frame) => Mathf.Acos(Mathf.Clamp(-pivotHeight / (frame.Length * Mathf.Max(0.1f, frame.PlaneUp.y)), -1f, 1f)) * Mathf.Rad2Deg;

		// 시각 -> 칼 각도. 당김, 첫 베기, 치켜듦, 둘째 베기
		private float SwordAngle(Frame frame, float now)
		{
			float back = firstSwing.x - windBack;
			if (now < windupSeconds)
				return Mathf.Lerp(firstSwing.x, back, UltimateFxRun.EaseOut(UltimateFxRun.Span(now, 0.05f, windupSeconds)));
			if (now < SlashEnd)
				return Mathf.Lerp(back, firstSwing.y, UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, windupSeconds, SlashEnd)));
			if (now < ChopStart)
				return Mathf.Lerp(firstSwing.y, raiseAngle, UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, SlashEnd + 0.1f, OpenEnd)));
			return Mathf.Lerp(raiseAngle, GroundAngle(frame), UltimateFxRun.EaseIn(UltimateFxRun.Span(now, ChopStart, ChopTime)));
		}

		private Material StarWindow(UltimateFxRun run, float rimAmount, float edgeAmount)
		{
			Material material = new(starWindowShader) { name = "UltimateFx_StarWindow" };
			material.SetColor("_SpaceColor", spaceColor);
			material.SetColor("_NebulaColor", nebulaColor);
			material.SetColor("_StarColor", starColor);
			material.SetColor("_RimColor", rimColor);
			material.SetColor("_WhiteColor", whiteColor);
			material.SetFloat("_StarScale", starScale);
			material.SetFloat("_StarAmount", starAmount);
			material.SetFloat(RIM_AMOUNT_ID, rimAmount);
			material.SetFloat(EDGE_AMOUNT_ID, edgeAmount);
			material.SetFloat(ALPHA_ID, 0f);
			run.OnRelease(() => Object.Destroy(material));
			return material;
		}

		// 칼: 별밭 창 칼날과 어두운 금속 자루. 휘두름 면 위에서 돎. 첫 베기 자취 (별밭 부채꼴), 칼날 점광
		private void ComposeSword(Stage stage, Frame frame, Light light)
		{
			UltimateFxRun run = stage.Run;
			GameObject sword = new("NightBladeSword");
			sword.transform.SetParent(run.Root, false);
			Material bladeMaterial = StarWindow(run, bladeRim, 0f);
			UltimateFxStage.CreateMeshObject("NightBlade", sword.transform, Keep(run, UltimateMeshFactory.Blade(14, bladeWidth, bladeWidth * 0.22f, bladeCurve)), bladeMaterial);
			// 인물 (큐 3000) 보다 먼저 그려 칼이 인물을 덮지 않게
			bladeMaterial.renderQueue = 2990;
			Material hiltMaterial = Glow(run, Color.white, Color.black, rimColor * 0.25f, true);
			hiltMaterial.SetFloat(SPECULAR_ID, 0.7f);
			GameObject hilt = UltimateFxStage.CreateMeshObject("NightBladeHilt", sword.transform, Keep(run, UltimateMeshFactory.Hilt(bladeWidth * 2.2f, bladeWidth * 0.7f, 0.2f, bladeWidth * 0.42f, new Color(0.5f, 0.42f, 0.2f, 0f), new Color(0.12f, 0.1f, 0.16f, 0f))), hiltMaterial);
			Solid(hilt, false);
			sword.SetActive(false);
			float fadeStart = ChopTime + 0.45f;
			float fadeSeconds = 0.5f;
			run.At(0.05f, () => sword.SetActive(true));
			run.Track(0.05f, fadeStart + fadeSeconds - 0.05f, _ =>
			{
				float now = run.Now;
				float appear = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, 0.05f, windupSeconds * 0.7f));
				float fade = 1f - UltimateFxRun.Span(now, fadeStart, fadeStart + fadeSeconds);
				float white = UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, AbsorbStart + (AbsorbEnd - AbsorbStart) * 0.3f, AbsorbEnd));
				sword.transform.SetPositionAndRotation(frame.Pivot, frame.Pose(SwordAngle(frame, now)));
				sword.transform.localScale = Vector3.one * frame.Length * Mathf.Lerp(0.7f, 1f, appear);
				bladeMaterial.SetFloat(ALPHA_ID, appear * fade);
				bladeMaterial.SetFloat(WHITE_ID, white * Mathf.Lerp(1f, 0.6f, UltimateFxRun.Span(now, ChopTime, fadeStart)));
				// 흰 날이 된 순간과 땅을 긋는 순간 번쩍
				bladeMaterial.SetFloat(BRIGHTNESS_ID, 1f + 0.8f * Spike(now, AbsorbEnd, 0.05f, 0.2f) + 1.2f * Spike(now, ChopTime, 0.02f, 0.2f));
				bladeMaterial.SetFloat(RIM_AMOUNT_ID, bladeRim * (1f + 1.5f * Spike(now, SlashTime, 0.02f, 0.15f)));
				hilt.SetActive(fade > 0.5f);
			}, () => run.Destroy(sword));

			ComposeTrail(stage, frame, firstSwing.x - windBack, firstSwing.y, windupSeconds + slashSeconds * 0.3f, 0.5f, false);
			ComposeTrail(stage, frame, raiseAngle, GroundAngle(frame), ChopStart + chopSeconds * 0.3f, 0.45f, true);

			run.Track(0.05f, ChopTime + 1.2f - 0.05f, _ =>
			{
				float now = run.Now;
				light.transform.position = frame.Pivot + frame.Aim(SwordAngle(frame, now)) * frame.Length * 0.5f + stage.ToCamera * 0.8f;
				float white = UltimateFxRun.Span(now, AbsorbStart, AbsorbEnd);
				light.color = Color.Lerp(bladeLightColor, Color.white, white);
				float hold = now < ChopTime ? Mathf.Lerp(bladeLightIntensity.x, bladeLightIntensity.y, white) * UltimateFxRun.Span(now, 0.05f, windupSeconds) : 0f;
				float slash = bladeLightIntensity.y * Spike(now, SlashTime, 0.02f, 0.12f);
				float chop = chopLightIntensity * Spike(now, ChopTime, 0.02f, 0.4f);
				light.intensity = Mathf.Max(hold, Mathf.Max(slash, chop));
			}, () => light.enabled = false);
		}

		// 휘두름 자취: 칼이 지나간 부채꼴. 첫 베기는 별밭 창 (가장자리 보라 빛), 둘째 베기는 흰 가산
		private void ComposeTrail(Stage stage, Frame frame, float fromDegrees, float toDegrees, float start, float seconds, bool white)
		{
			UltimateFxRun run = stage.Run;
			Mesh mesh = Keep(run, UltimateMeshFactory.SwingFan(28, toDegrees - fromDegrees, 0.55f));
			Material material = white ? run.Additive(UltimateFxTextures.White, Color.clear) : StarWindow(run, 0f, 1.4f);
			GameObject trail = UltimateFxStage.CreateMeshObject("NightBladeTrail", run.Root, mesh, material);
			material.renderQueue = 2988;
			trail.transform.SetPositionAndRotation(frame.Pivot, frame.Pose(fromDegrees));
			trail.transform.localScale = Vector3.one * frame.Length;
			trail.SetActive(false);
			run.At(start, () => trail.SetActive(true));
			run.Track(start, seconds, progress =>
			{
				float level = Mathf.Pow(1f - progress, 1.6f);
				if (white)
					material.SetColor("_Color", whiteColor * trailBrightness.y * level);
				else
					material.SetFloat(ALPHA_ID, trailBrightness.x * level);
			}, () => run.Destroy(trail));
		}

		private void ComposeGrade(Stage stage)
		{
			stage.BloomTint = new Color(0.8f, 0.7f, 1f);
			// 밤: 틈이 열리며 어두워지고 둘째 베기에 곧바로 낮으로
			float Night(float now) => Plateau(now, OpenStart, openSeconds, ChopTime, restoreSeconds);
			float Tense(float now) => Plateau(now, 0.05f, windupSeconds, ChopTime, restoreSeconds);
			stage.SunScale = now => Mathf.Lerp(Mathf.Lerp(1f, 0.75f, Tense(now)), sunDim, Night(now));
			stage.AmbientScale = now => Mathf.Lerp(Mathf.Lerp(1f, 0.8f, Tense(now)), ambientDim, Night(now));
			stage.Saturation = now => slashSaturation * Window(now, SlashTime, 0.2f);
			stage.Vignette = now => vignette * Mathf.Max(Night(now), 0.4f * Tense(now));
			stage.Filter = now => Color.Lerp(Color.white, nightTint, Night(now));
			stage.Bloom = now => bloom * Mathf.Max(0.4f * Tense(now), Plateau(now, SlashTime, 0.05f, ChopTime + 0.6f, 0.8f));
		}
	}
}
