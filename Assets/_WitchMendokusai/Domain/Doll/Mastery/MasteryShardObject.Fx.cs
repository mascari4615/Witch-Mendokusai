using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 숙련 조각의 잘 보이게 하는 장치 (사용자 2026-11-23 시안 5 맥박 고리)
	// 바닥: 막대 색 빛 원 하나 (숨쉬기) + 안에서 밖으로 퍼지며 사라지는 고리 여럿 (어긋난 박자)
	// 둘레: 보석 둘레를 돌며 깜빡이는 작은 네 갈래 별
	// 자식은 처음 Init 에서 코드가 만듦 (프리팹에 칸을 손으로 쓰지 않게). 모양과 박자 값은 프리팹 직렬화
	public partial class MasteryShardObject
	{
		private static readonly int FX_COLOR = Shader.PropertyToID("_Color");
		private static readonly int FX_ALPHA = Shader.PropertyToID("_Alpha");
		private static Mesh groundQuad;

		[Header("바닥 고리")]
		[SerializeField, Tooltip("퍼지는 고리 재질 (WM/SurvivalWeaponFx Field)")] private Material ringMaterial;
		[SerializeField, Tooltip("바닥 빛 원 재질 (WM/SurvivalWeaponFx Field)")] private Material groundGlowMaterial;
		[SerializeField, Min(0)] private int ringCount = 2;
		[SerializeField, Tooltip("고리 한 번 퍼지는 시간 (초)"), Min(0.1f)] private float ringPeriod = 1.6f;
		[SerializeField, Tooltip("고리 시작 지름 (m)"), Min(0f)] private float ringStartSize = .5f;
		[SerializeField, Tooltip("고리 끝 지름 (m)"), Min(0f)] private float ringEndSize = 2f;
		[SerializeField, Tooltip("퍼지는 곡선 (0~1 시간 -> 0~1 크기)")] private AnimationCurve ringGrowth = new(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
		[SerializeField, Tooltip("사라지는 곡선 (0~1 시간 -> 투명도)")] private AnimationCurve ringFade = new(new Keyframe(0f, 0f), new Keyframe(.15f, 1f), new Keyframe(1f, 0f));
		[SerializeField, Tooltip("바닥 빛 원 지름 (m)"), Min(0f)] private float groundGlowSize = 1.1f;
		[SerializeField, Tooltip("지면에서 띄우는 높이 (겹침 방지)"), Min(0f)] private float groundLift = .03f;
		[SerializeField, Tooltip("고리끼리 높이 차 (z 싸움 방지)"), Min(0f)] private float ringLayerStep = .004f;

		[Header("반짝 별")]
		[SerializeField, Tooltip("별 재질 (WM/MasteryGem, 밝은 색)")] private Material sparkleMaterial;
		[SerializeField] private List<CrystalSpec> sparkleRays = new();
		[SerializeField, Min(0)] private int sparkleCount = 2;
		[SerializeField, Tooltip("보석 중심에서 별까지 (m)"), Min(0f)] private float sparkleRadius = .42f;
		[SerializeField, Tooltip("초당 공전 각도")] private float sparkleOrbitSpeed = 60f;
		[SerializeField, Tooltip("한 번 깜빡이는 시간 (초)"), Min(0.05f)] private float sparkleTwinkleSeconds = 1.2f;
		[SerializeField, Tooltip("별 최대 크기 배율"), Min(0f)] private float sparkleSize = 1f;
		[SerializeField, Tooltip("공전하며 오르내리는 폭 (반경 비율)"), Min(0f)] private float sparkleWave = .35f;

		private readonly List<MeshRenderer> rings = new();
		private readonly List<Transform> sparkles = new();
		private MeshRenderer groundGlow;
		private MaterialPropertyBlock fxBlock;
		private float groundY;

		private void InitFx(Color color, float groundDepth)
		{
			fxBlock ??= new MaterialPropertyBlock();
			groundY = -groundDepth + groundLift;
			if (groundGlow == null)
			{
				groundGlow = CreateGroundQuad("[Fx] GroundGlow", groundGlowMaterial);
				for (int i = 0; i < ringCount; i++)
					rings.Add(CreateGroundQuad("[Fx] Ring", ringMaterial));
				Mesh starMesh = SparkleMesh();
				for (int i = 0; i < sparkleCount; i++)
				{
					GameObject star = new("[Fx] Sparkle");
					star.transform.SetParent(transform, false);
					star.AddComponent<MeshFilter>().sharedMesh = starMesh;
					MeshRenderer starRenderer = star.AddComponent<MeshRenderer>();
					starRenderer.sharedMaterial = sparkleMaterial;
					starRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
					sparkles.Add(star.transform);
				}
			}
			SetFx(groundGlow, color, 0f);
			foreach (MeshRenderer ring in rings)
				SetFx(ring, color, 0f);
		}

		private MeshRenderer CreateGroundQuad(string objectName, Material material)
		{
			GameObject quad = new(objectName);
			quad.transform.SetParent(transform, false);
			quad.AddComponent<MeshFilter>().sharedMesh = GroundQuad();
			MeshRenderer quadRenderer = quad.AddComponent<MeshRenderer>();
			quadRenderer.sharedMaterial = material;
			quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			quadRenderer.receiveShadows = false;
			return quadRenderer;
		}

		private void SetFx(MeshRenderer target, Color color, float alpha)
		{
			target.GetPropertyBlock(fxBlock);
			fxBlock.SetColor(FX_COLOR, color);
			fxBlock.SetFloat(FX_ALPHA, alpha);
			target.SetPropertyBlock(fxBlock);
		}

		private void UpdateFx(float time, float pop, float breath)
		{
			groundGlow.transform.localPosition = Vector3.up * groundY;
			groundGlow.transform.localScale = Vector3.one * (groundGlowSize * pop);
			SetFx(groundGlow, trackColor, breath);

			for (int i = 0; i < rings.Count; i++)
			{
				float cycle = Mathf.Repeat(time / ringPeriod + (float)i / rings.Count, 1f);
				float size = Mathf.Lerp(ringStartSize, ringEndSize, ringGrowth.Evaluate(cycle));
				Transform ring = rings[i].transform;
				ring.localPosition = Vector3.up * (groundY + ringLayerStep * (i + 1));
				ring.localScale = Vector3.one * (size * pop);
				SetFx(rings[i], trackColor, ringFade.Evaluate(cycle) * pop);
			}

			Vector3 center = shardTransform.localPosition;
			for (int i = 0; i < sparkles.Count; i++)
			{
				float angle = (time * sparkleOrbitSpeed + 360f * i / sparkles.Count) * Mathf.Deg2Rad;
				sparkles[i].localPosition = center + new Vector3(Mathf.Cos(angle), sparkleWave * Mathf.Sin(angle * 2f), Mathf.Sin(angle)) * sparkleRadius;
				float twinkle = Mathf.Sin((time / sparkleTwinkleSeconds + (float)i / sparkles.Count) * Mathf.PI);
				sparkles[i].localScale = Vector3.one * (sparkleSize * pop * twinkle * twinkle);
			}
		}

		private Mesh SparkleMesh()
		{
			string meshName = clusterName + "Sparkle";
			if (MESH_CACHE.TryGetValue(meshName, out Mesh mesh) && mesh != null)
				return mesh;
			mesh = CrystalClusterMesh.Build(sparkleRays, facetShade, meshName);
			MESH_CACHE[meshName] = mesh;
			return mesh;
		}

		// 바닥에 누운 1m 사각 (uv 0~1). SurvivalWeaponFx Field 가 uv 로 원을 그림
		private static Mesh GroundQuad()
		{
			if (groundQuad != null)
				return groundQuad;
			groundQuad = new Mesh { name = "MasteryShardGroundQuad" };
			groundQuad.SetVertices(new List<Vector3> { new(-.5f, 0f, -.5f), new(.5f, 0f, -.5f), new(.5f, 0f, .5f), new(-.5f, 0f, .5f) });
			groundQuad.SetUVs(0, new List<Vector2> { new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f) });
			groundQuad.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
			groundQuad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
			groundQuad.RecalculateBounds();
			return groundQuad;
		}
	}
}
