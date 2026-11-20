using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 창 왕관 약한 판 (일반 스킬용). 시전자 앞에서 표적 쪽으로 결정 가시 한 줄이 차례로 솟았다 깨짐
	/// - 0.8초 (솟음 0.3, 유지 0.2, 깨짐 0.3). 광원 1개 (줄 앞을 따르는 결정 점광), 장면 빛 변경 없음, 후처리는 빛 번짐만
	/// - 수십 번 반복돼도 시야를 가리지 않게 낮은 결정 (인물 1.5~2.5배)
	/// - 공통 부품은 SpearCrownWorldFx. 강한 판은 SpearCrownHeavyUltimateFx (별개 에셋)
	/// - 재생: 개발 창 fx ultimate SpearCrownLight
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_SpearCrownLight", menuName = "WM/CombatFx/Ultimate/" + nameof(SpearCrownLightUltimateFx))]
	public sealed class SpearCrownLightUltimateFx : SpearCrownWorldFx
	{
		[Header("배열")]
		[SerializeField, Min(1)] private int crystalCount = 7;
		[SerializeField, Min(0f), Tooltip("시전자에서 첫 결정까지")] private float firstDistance = 0.9f;
		[SerializeField, Min(0.1f)] private float spacing = 0.85f;
		[SerializeField, Tooltip("첫 결정과 마지막 결정 높이")] private Vector2 heightRange = new(1.4f, 2.6f);
		[SerializeField] private Vector2 widthRange = new(1.15f, 1.45f);
		[SerializeField, Min(0f)] private float sideJitter = 0.3f;

		[Header("시간 (초)")]
		[SerializeField, Min(0f), Tooltip("결정 사이 솟는 간격")] private float riseInterval = 0.04f;
		[SerializeField, Min(0.02f)] private float riseSeconds = 0.1f;
		[SerializeField, Min(0.1f), Tooltip("깨지는 시각")] private float breakTime = 0.5f;
		[SerializeField, Min(0.05f)] private float breakSeconds = 0.3f;
		[SerializeField, Min(0.1f)] private float shardLinger = 0.9f;
		[SerializeField, Min(0.1f)] private float crackLinger = 1.3f;

		[Header("빛과 입자")]
		[SerializeField, Min(0f)] private float lightIntensity = 12f;
		[SerializeField, Min(1f)] private float lightRange = 6f;
		[SerializeField, Min(0f)] private float lightHeight = 1.2f;
		[SerializeField, Min(0f)] private float bloom = 0.7f;
		[SerializeField, Min(0)] private int shardsPerUnitHeight = 3;
		[SerializeField, Min(0f)] private float shardSpeed = 4f;
		[SerializeField, Min(0)] private int sparklePerCrystal = 8;
		[SerializeField, Min(0)] private int dustPerCrystal = 6;
		[SerializeField, Min(0f)] private float shakeStrength = 0.4f;

		public override float DurationSeconds => breakTime + breakSeconds;

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Mesh[] meshes = CrystalMeshes(run);
			Material material = CrystalMaterial(run);
			Vector3 forward = stage.Target - run.Caster;
			forward.y = 0f;
			forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : run.ScreenForward;
			Vector3 side = Vector3.Cross(Vector3.up, forward);

			Texture2D crack = CrackTexture(run, 0, 0.5f);
			List<CrystalPiece> pieces = new();
			for (int i = 0; i < crystalCount; i++)
			{
				float ratio = crystalCount <= 1 ? 1f : i / (float)(crystalCount - 1);
				Vector3 position = run.Caster + forward * (firstDistance + i * spacing) + side * Random.Range(-sideJitter, sideJitter);
				float height = Mathf.Lerp(heightRange.x, heightRange.y, ratio) * Random.Range(0.9f, 1.1f);
				// 줄 바깥 (옆) 으로 기울어 가시 줄이 갈래로 보이게
				Vector3 outward = side * (i % 2 == 0 ? 1f : -1f) + forward * 0.4f;
				CrystalPiece piece = PlaceCrystal(run, meshes[i % meshes.Length], material, position, outward, height, Random.Range(widthRange.x, widthRange.y), true);
				pieces.Add(piece);
				float start = i * riseInterval;
				Rise(run, piece, start, riseSeconds);
				run.At(start, () =>
				{
					Puff(run, piece.Raised, dustPerCrystal, 0.9f);
					Sparkle(run, piece.Raised, sparklePerCrystal, 0.4f, 0.7f);
				});
				Crack(run, crack, piece.Raised, height * 1.1f, start, breakTime + crackLinger, 0.6f);
			}

			// 줄 앞을 따르는 결정 점광 하나. 깨질 때 한 번 더 밝아지고 꺼짐
			Light front = WorldLight(stage, "CrystalLight", LightType.Point, CoreLightColor, lightRange);
			float lastRise = (crystalCount - 1) * riseInterval + riseSeconds;
			run.Track(0f, DurationSeconds, progress =>
			{
				float now = run.Now;
				float along = Mathf.Clamp01(now / Mathf.Max(0.01f, lastRise));
				Vector3 position = run.Caster + forward * (firstDistance + along * (crystalCount - 1) * spacing * 0.75f);
				front.transform.position = position + Vector3.up * lightHeight;
				float rise = Mathf.Clamp01(now / 0.08f);
				float hold = Mathf.Lerp(1f, 0.55f, UltimateFxRun.Span(now, lastRise, breakTime));
				float burst = 0.8f * Spike(now, breakTime, 0.04f, 0.12f);
				front.intensity = lightIntensity * rise * (hold + burst) * (1f - UltimateFxRun.Span(now, breakTime + 0.1f, DurationSeconds));
			}, () => front.enabled = false);

			Shatter(run, material, meshes[0], pieces, breakTime, shardsPerUnitHeight, shardSpeed, shardLinger, breakSeconds);
			Vector3 lineMiddle = run.Caster + forward * (firstDistance + (crystalCount - 1) * spacing * 0.5f);
			GroundGlow(run, lineMiddle, (crystalCount - 1) * spacing + 2f, CoreLightColor * 0.7f, 0f, lastRise, breakTime, DurationSeconds + 0.6f);
			run.At(0f, () => run.Shake(shakeStrength));
			run.At(breakTime, () => run.Shake(shakeStrength * 0.6f));

			stage.BloomTint = CoreLightColor;
			stage.Bloom = now => bloom * Mathf.Clamp01(now / 0.1f) * (1f - UltimateFxRun.Span(now, breakTime, DurationSeconds + 0.4f));
		}
	}
}
