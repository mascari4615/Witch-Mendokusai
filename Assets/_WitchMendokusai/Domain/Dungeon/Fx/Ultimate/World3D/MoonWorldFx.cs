using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 달 가르기 3D 의 공통 부품. 약한 판 (MoonLightUltimateFx) 과 강한 판 (MoonHeavyUltimateFx) 이 같이 씀
	/// - 달 메시 (코드 생성: 구덩이를 실제로 판 구, 어두운 바다 무늬, 미리 사선으로 자른 두 반쪽과 단면 뚜껑), 표면과 단면 재질, 검기 줄, 단면 빛 알갱이, 달 조각
	/// - 외부 달 지도 없이 코드로 표면을 만듦 (CC BY 표기 의무 없음). 요철은 실제 정점 높이라 옆 광원에서 음영이 생김
	/// - 설계: memo wm/design/ult3d-all-20261120/04_moon.md
	/// </summary>
	public abstract class MoonWorldFx : WorldUltimateFx
	{
		protected sealed class MoonBody
		{
			public Transform Pivot;
			public Transform Upper;
			public Transform Lower;
			public Material Surface;
			public Material Cap;
			/// <summary> 달 지역 공간의 단면 법선 (윗쪽 반쪽 방향) </summary>
			public Vector3 CutNormal;
			/// <summary> 달 지역 공간의 단면 위 선 방향 (화면에서 사선) </summary>
			public Vector3 CutLine;
		}

		[Header("달 표면")]
		[SerializeField] private int surfaceSeed = 7;
		[SerializeField, Min(8)] private int surfaceRings = 40;
		[SerializeField, Min(16)] private int surfaceSegments = 96;
		[SerializeField, Min(0f), Tooltip("구덩이 깊이 배율")] private float craterDepth = 0.55f;
		[SerializeField] private Color surfaceColor = new(0.6f, 0.61f, 0.66f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color surfaceRim = new(0.12f, 0.2f, 0.4f);
		[SerializeField, Range(0f, 1f)] private float surfaceSpecular = 0.08f;
		[SerializeField, ColorUsage(false, true)] private Color capGlow = new(3.2f, 3.6f, 4.4f);
		[SerializeField, Range(0f, 90f), Tooltip("단면이 화면 가로에서 기운 각도")] private float cutAngle = 35f;

		[Header("빛과 입자 색")]
		[SerializeField] private Color moonLightColor = new(0.62f, 0.78f, 1f, 1f);
		[SerializeField] private Color sectionLightColor = new(0.92f, 0.96f, 1f, 1f);
		[SerializeField, ColorUsage(true, true)] private Color slashColor = new(2.4f, 2.8f, 3.6f, 1f);
		[SerializeField, ColorUsage(true, true)] private Color grainColor = new(2.2f, 2.5f, 3.2f, 1f);
		[SerializeField] private Color rockColor = new(0.55f, 0.56f, 0.6f, 1f);
		[SerializeField] private Color dustColor = new(0.62f, 0.62f, 0.66f, 0.7f);
		[SerializeField] private Color craterColor = new(0.07f, 0.07f, 0.09f, 0.88f);
		[SerializeField] private Color craterLineColor = new(0.55f, 0.75f, 1f, 1f);
		[SerializeField] private int craterSeed = 13;

		protected Color MoonLightColor => moonLightColor;
		protected Color SectionLightColor => sectionLightColor;
		protected Color GrainColor => grainColor;
		protected Color DustColor => dustColor;
		protected Color CapGlow => capGlow;

		/// <summary> 달 하나 (두 반쪽이 맞붙은 상태). 앞이 카메라를 봄. 반지름 1 이 기본, Pivot 배율로 크기 </summary>
		protected MoonBody CreateMoon(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float radians = cutAngle * Mathf.Deg2Rad;
			Vector3 cutNormal = new(-Mathf.Sin(radians), Mathf.Cos(radians), 0f);
			Vector3 cutLine = new(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
			Material surface = Glow(run, surfaceColor, Color.black, surfaceRim, true);
			surface.SetFloat(SPECULAR_ID, surfaceSpecular);
			surface.SetFloat(SMOOTHNESS_ID, 8f);
			surface.SetFloat(RIM_POWER_ID, 3.5f);
			surface.SetVector(DISSOLVE_RANGE_ID, new Vector4(-1.15f, 1.15f, 0f, 0f));
			surface.SetColor(DISSOLVE_EDGE_ID, grainColor);
			Material cap = Glow(run, new Color(0.9f, 0.95f, 1f, 1f), Color.black, Color.black, true);
			cap.SetVector(DISSOLVE_RANGE_ID, new Vector4(-1.15f, 1.15f, 0f, 0f));
			cap.SetColor(DISSOLVE_EDGE_ID, grainColor);

			GameObject pivot = new("Moon");
			pivot.transform.SetParent(run.Root, false);
			pivot.transform.rotation = Quaternion.LookRotation(-stage.ToCamera);
			Transform Half(bool positive)
			{
				Mesh mesh = Keep(run, UltimateMeshFactory.MoonHalf(surfaceSeed, cutNormal, positive, surfaceRings, surfaceSegments, craterDepth));
				GameObject half = UltimateFxStage.CreateMeshObject(positive ? "MoonUpper" : "MoonLower", pivot.transform, mesh, surface);
				MeshRenderer meshRenderer = Solid(half, true);
				meshRenderer.sharedMaterials = new[] { surface, cap };
				return half.transform;
			}
			MoonBody body = new()
			{
				Pivot = pivot.transform,
				Upper = Half(true),
				Lower = Half(false),
				Surface = surface,
				Cap = cap,
				CutNormal = cutNormal,
				CutLine = cutLine,
			};
			pivot.SetActive(false);
			return body;
		}

		/// <summary> 녹아 나타나거나 (amount 1 -> 0) 사라짐. direction 1 이면 아래부터 남고 위로 녹음 </summary>
		protected static void SetDissolve(MoonBody body, float amount, float direction)
		{
			body.Surface.SetFloat(DISSOLVE_DIRECTION_ID, direction);
			body.Surface.SetFloat(DISSOLVE_ID, amount);
			body.Cap.SetFloat(DISSOLVE_DIRECTION_ID, direction);
			body.Cap.SetFloat(DISSOLVE_ID, amount);
		}

		/// <summary> 월드 공간 검기: 시전자에서 달을 지나는 사선의 늘어진 빛 줄 </summary>
		protected void Slash(UltimateFxRun run, Vector3 from, Vector3 through, int count, float speed, float width)
		{
			ParticleSystem streaks = Streaks(run, "MoonSlash", count + 8, 0.035f);
			Vector3 direction = (through - from).normalized;
			float length = Vector3.Distance(from, through) * 1.35f;
			for (int i = 0; i < count; i++)
			{
				Vector3 position = from + direction * length * Random.Range(0f, 1f) + Random.insideUnitSphere * width;
				UltimateFxStage.Emit(streaks, position, direction * speed * Random.Range(0.8f, 1.2f), Random.Range(0.12f, 0.26f), Random.Range(0.1f, 0.2f), slashColor);
			}
		}

		/// <summary> 단면에서 떨어지는 빛 알갱이 </summary>
		protected static ParticleSystem Grains(UltimateFxRun run, int maxParticles) => Motes(run, "MoonGrains", maxParticles + 8, 0.25f, 0.5f);

		/// <summary> 달 조각 메시 입자 (월드 충돌, 바닥에서 튐) </summary>
		protected ParticleSystem Fragments(UltimateFxRun run, int maxParticles, out Mesh rockMesh)
		{
			rockMesh = Keep(run, UltimateMeshFactory.Rock(surfaceSeed + 5, rockColor, 0.5f));
			Material material = Glow(run, Color.white, new Color(0.5f, 0.7f, 1.2f), surfaceRim, true);
			ParticleSystem fragments = MeshParticles(run, "MoonFragments", rockMesh, material, maxParticles + 8, 2f, true, 0.3f, 0.5f, true);
			UltimateFxStage.SetLifetimeLook(fragments, null, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
			return fragments;
		}

		protected Material RockMaterial(UltimateFxRun run) => Glow(run, Color.white, Color.black, surfaceRim * 0.5f, true);

		protected Mesh RockMesh(UltimateFxRun run, int seedOffset) => Keep(run, UltimateMeshFactory.Rock(surfaceSeed + 11 + seedOffset, rockColor, 0.55f));

		/// <summary> 달 조각이 박힌 구덩이 (그을림 대신 짙은 원과 청색 금) </summary>
		protected Texture2D CraterTexture(UltimateFxRun run, int seedOffset) => ScorchTexture(run, craterSeed + seedOffset, craterColor, craterLineColor, 0.55f);

		protected static void Crater(UltimateFxRun run, Texture2D texture, Vector3 position, float size, float appear, float lingerEnd, float fadeOut) => GroundDecal(run, texture, position, size, appear, 0.05f, lingerEnd, fadeOut, 1);

		protected void Puff(UltimateFxRun run, Vector3 origin, int count, float radius) => DustRing(run, origin, count, radius, dustColor, 1.2f);
	}
}
