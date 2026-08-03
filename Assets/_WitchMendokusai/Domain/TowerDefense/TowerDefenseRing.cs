using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 바닥에 그리는 원 — 포탑 사거리·노드 점유 반경처럼 「어디까지 닿는가」를 눈으로 보여준다(TASK-WM-194).
	///
	/// ★ 왜 필요한가: 사거리가 안 보이면 배치는 감(勘)이다. 지형이 길목을 만들어도, 그 길목이 내 포탑
	///   사거리 안인지 모르면 「여기 지으면 되겠다」라는 판단 자체가 성립하지 않는다.
	///
	/// LineRenderer 1개 = 원 1개. 아트 에셋 0(런타임 셰이더 조회) — 색·굵기·반지름 전부 코드에서 준다.
	/// </summary>
	public sealed class TowerDefenseRing : MonoBehaviour
	{
		private const int SEGMENTS = 48;

		private LineRenderer lineRenderer;
		private float radius = -1f;

		/// <summary> parent 아래에 원 하나를 만든다(로컬 원점 기준). </summary>
		public static TowerDefenseRing Create(Transform parent, string name, Color color, float width, float heightOffset)
		{
			GameObject ringObject = new GameObject(name);
			ringObject.transform.SetParent(parent, false);
			ringObject.transform.localPosition = new Vector3(0f, heightOffset, 0f);

			TowerDefenseRing ring = ringObject.AddComponent<TowerDefenseRing>();
			ring.lineRenderer = ringObject.AddComponent<LineRenderer>();
			ring.lineRenderer.useWorldSpace = false;
			ring.lineRenderer.loop = true;
			ring.lineRenderer.positionCount = SEGMENTS;
			ring.lineRenderer.widthMultiplier = width;
			ring.lineRenderer.numCapVertices = 2;
			ring.lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			ring.lineRenderer.receiveShadows = false;
			ring.lineRenderer.material = CreateLineMaterial(color);
			ring.lineRenderer.startColor = color;
			ring.lineRenderer.endColor = color;
			return ring;
		}

		public void SetRadius(float newRadius)
		{
			if (Mathf.Approximately(radius, newRadius))
				return;

			radius = newRadius;
			for (int index = 0; index < SEGMENTS; index++)
			{
				float angle = index * Mathf.PI * 2f / SEGMENTS;
				lineRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle) * newRadius, 0f, Mathf.Sin(angle) * newRadius));
			}
		}

		public void SetColor(Color color)
		{
			lineRenderer.startColor = color;
			lineRenderer.endColor = color;
		}

		public void SetVisible(bool visible)
		{
			if (lineRenderer.enabled != visible)
				lineRenderer.enabled = visible;
		}

		// URP 환경에서 항상 잡히는 순서로 셰이더를 찾는다 — 못 찾으면 라인이 분홍(missing)으로 뜨는데,
		// 그건 「사거리가 안 보임」보다 더 나쁜 화면이라 마지막엔 기본 스프라이트 셰이더까지 훑는다.
		private static Material CreateLineMaterial(Color color)
		{
			// ★ 사거리 원은 *무엇에도 가리지 않는다*(사용자 지시 2회). 기성 셰이더에 깊이 옵션을 코드로
			//   꽂는 방식은 그 셰이더가 해당 속성을 안 가지면 **조용히 무시**된다 — 실제로 그래서 벽에
			//   계속 가렸다. 「깊이 검사 없음」이 못으로 박힌 전용 셰이더를 쓰고, 없을 때만 기성으로 내려간다.
			Shader shader = Shader.Find(TowerDefenseShaderNames.OverlayLine);
			if (shader == null)
			{
				Debug.LogWarning($"{nameof(TowerDefenseRing)}: 전용 오버레이 셰이더를 못 찾음 — 원이 다른 것에 가릴 수 있다.");
				Material fallback = TowerDefenseVisuals.CreateUnlit();
				fallback.color = color;
				return fallback;
			}

			Material material = new Material(shader);
			material.color = color;
			if (material.HasProperty("_BaseColor"))
				material.SetColor("_BaseColor", color);

			if (material.HasProperty("_ZTest"))
				material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
			if (material.HasProperty("_ZWrite"))
				material.SetInt("_ZWrite", 0);
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
			return material;
		}
	}
}
