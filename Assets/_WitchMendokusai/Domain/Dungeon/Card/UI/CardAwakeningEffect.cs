using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 카드 확정 순간의 국소 연출, 공격 판정과 무관
	public class CardAwakeningEffect : MonoBehaviour
	{
		[SerializeField] private Material effectMaterial;
		[SerializeField, Min(0.01f)] private float gatherDuration = 0.55f;
		[SerializeField, Min(0.01f)] private float releaseDuration = 0.16f;
		[SerializeField, Min(0.01f)] private float fadeDuration = 0.65f;
		[SerializeField, Min(3)] private int bladeCount = 6;
		[SerializeField, Min(12)] private int ringSegments = 64;
		[SerializeField] private float gatherRadius = 2.5f;
		[SerializeField] private float focusRadius = 1.2f;
		[SerializeField] private float releaseRadius = 4.6f;
		[SerializeField] private float bladeLength = 0.8f;
		[SerializeField] private float bladeWidth = 0.14f;
		[SerializeField] private float lineWidth = 0.025f;
		[SerializeField] private float orbitDegrees = 100f;
		[SerializeField] private float heightOffset = 0.7f;
		[SerializeField] private float bladeLift = 1.3f;
		[SerializeField, Range(0, 1)] private float dimOpacity = 0.28f;
		[SerializeField] private Color edgeColor = new(0.25f, 0.65f, 1f, 1f);
		[SerializeField] private Color coreColor = new(0.85f, 0.95f, 1f, 1f);

		private readonly List<Vector3> vertices = new(4096);
		private readonly List<Color> colors = new(4096);
		private readonly List<int> triangles = new(8192);
		private Mesh effectMesh;
		private Transform actor;
		private Camera worldCamera;
		private TimeManager timeManager;
		private Image dimImage;
		private float elapsed;
		private bool playing;

		public void Play(Transform target, TimeManager clock)
		{
			actor = target;
			worldCamera = Camera.main;
			timeManager = clock;
			elapsed = 0;
			playing = true;
			effectMesh = new Mesh { name = "Card awakening" };
			effectMesh.MarkDynamic();
			gameObject.AddComponent<MeshFilter>().sharedMesh = effectMesh;
			MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = effectMaterial;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			GameObject shade = new("Awakening shade", typeof(RectTransform), typeof(Canvas));
			shade.transform.SetParent(transform, false);
			Canvas canvas = shade.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = -100;
			GameObject imageObject = new("Shade", typeof(RectTransform), typeof(Image));
			imageObject.transform.SetParent(shade.transform, false);
			dimImage = imageObject.GetComponent<Image>();
			dimImage.raycastTarget = false;
			dimImage.rectTransform.anchorMin = Vector2.zero;
			dimImage.rectTransform.anchorMax = Vector2.one;
			dimImage.rectTransform.offsetMin = Vector2.zero;
			dimImage.rectTransform.offsetMax = Vector2.zero;
			timeManager.Pause(gameObject);
			Draw();
		}

		private void Update()
		{
			if (playing == false) return;
			elapsed += Time.unscaledDeltaTime;
			if (actor == null || elapsed >= gatherDuration + releaseDuration + fadeDuration)
			{
				Destroy(gameObject);
				return;
			}
			Draw();
		}

		private void Draw()
		{
			transform.position = actor.position + Vector3.up * heightOffset;
			transform.rotation = Quaternion.identity;
			vertices.Clear();
			colors.Clear();
			triangles.Clear();
			float gather = Mathf.Clamp01(elapsed / gatherDuration);
			float release = Mathf.Clamp01((elapsed - gatherDuration) / releaseDuration);
			float fade = Mathf.Clamp01((elapsed - gatherDuration - releaseDuration) / fadeDuration);
			float intensity = Mathf.SmoothStep(0, 1, gather) * (1 - fade);
			float radius = Mathf.Lerp(gatherRadius, focusRadius, gather * gather);
			radius = Mathf.Lerp(radius, releaseRadius, 1 - (1 - release) * (1 - release));
			dimImage.color = new Color(0, 0, 0, dimOpacity * intensity);
			float rotation = orbitDegrees * gather * gather * Mathf.Deg2Rad;
			Ring(radius, lineWidth, edgeColor, intensity);
			Ring(radius * 0.86f, lineWidth * 0.5f, coreColor, intensity * 0.7f);
			for (int i = 0; i < bladeCount; i++)
			{
				float angle = rotation + i * Mathf.PI * 2 / bladeCount;
				Vector3 direction = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
				Vector3 center = direction * radius + Vector3.up * bladeLift * gather;
				Blade(center, Vector3.Lerp(Vector3.up, direction, release).normalized, intensity);
				if (release > 0)
				{
					Line(direction * focusRadius + Vector3.up * bladeLift, center - direction * bladeLength, lineWidth, edgeColor, (1 - fade) * (1 - fade), true);
				}
			}
			if (release > 0)
			{
				float burst = 1 - fade;
				for (int i = 0; i < bladeCount * 4; i++)
				{
					float angle = i * Mathf.PI * 2 / (bladeCount * 4) + rotation;
					Vector3 direction = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
					float distance = Mathf.Lerp(focusRadius, releaseRadius, release) + fade * (i % 3 + 1);
					Line(direction * distance, direction * (distance + bladeLength * burst * 0.4f), lineWidth, coreColor, burst * burst);
				}
			}
			effectMesh.Clear();
			effectMesh.SetVertices(vertices);
			effectMesh.SetColors(colors);
			effectMesh.SetTriangles(triangles, 0);
			effectMesh.RecalculateBounds();
		}

		private void Blade(Vector3 center, Vector3 direction, float intensity)
		{
			Vector3 side = Vector3.Cross(worldCamera.transform.forward, direction).normalized;
			Triangle(center + direction * bladeLength, center + side * bladeWidth, center - direction * bladeLength * 0.5f, edgeColor, intensity);
			Triangle(center + direction * bladeLength, center - direction * bladeLength * 0.5f, center - side * bladeWidth, coreColor, intensity);
			Line(center - direction * bladeLength * 0.45f - side * bladeWidth * 2, center - direction * bladeLength * 0.45f + side * bladeWidth * 2, lineWidth * 2, coreColor, intensity, true);
			Line(center - direction * bladeLength * 0.5f, center - direction * bladeLength * 0.85f, lineWidth * 2, coreColor, intensity, true);
		}

		private void Ring(float radius, float width, Color color, float alpha)
		{
			for (int i = 0; i < ringSegments; i++)
			{
				float angle = i * Mathf.PI * 2 / ringSegments;
				float next = (i + 1) * Mathf.PI * 2 / ringSegments;
				Line(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius, new Vector3(Mathf.Cos(next), 0, Mathf.Sin(next)) * radius, width, color, alpha);
			}
		}

		private void Line(Vector3 from, Vector3 to, float width, Color color, float alpha, bool faceCamera = false)
		{
			Vector3 delta = (to - from).normalized;
			Vector3 side = Vector3.Cross(faceCamera ? worldCamera.transform.forward : Vector3.up, delta).normalized * width;
			Triangle(from - side, from + side, to + side, color, alpha);
			Triangle(from - side, to + side, to - side, color, alpha);
		}

		private void Triangle(Vector3 first, Vector3 second, Vector3 third, Color color, float alpha)
		{
			int start = vertices.Count;
			vertices.Add(first);
			vertices.Add(second);
			vertices.Add(third);
			color.a *= alpha;
			colors.Add(color);
			colors.Add(color);
			colors.Add(color);
			triangles.Add(start);
			triangles.Add(start + 1);
			triangles.Add(start + 2);
		}

		private void OnDisable()
		{
			playing = false;
			if (timeManager != null) timeManager.Resume(gameObject);
		}

		private void OnDestroy()
		{
			if (effectMesh != null) Destroy(effectMesh);
		}
	}
}
