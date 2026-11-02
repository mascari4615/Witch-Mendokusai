using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 실제 검 스프라이트를 호로 휘두르고, 칼날이 지나간 자리만 짧게 남기는 연출
	// 판정과 독립된 수명, 소유자 위치를 따라가고 방향은 생성 시점 고정
	public class SwordSwing : MonoBehaviour
	{
		[SerializeField] private SpriteRenderer blade;
		[SerializeField] private MeshFilter trailFilter;
		[SerializeField] private MeshRenderer trailRenderer;

		// 스프라이트 피벗 기준 손잡이와 칼끝 위치, 유닛
		[SerializeField] private Vector2 spriteGrip = new(-0.25f, 0.25f);
		[SerializeField] private Vector2 spriteTip = new(0.458f, -0.458f);

		[SerializeField, Min(0.1f)] private float bladeLength = 1.8f;
		[SerializeField] private float handRadius = 0.35f;
		[SerializeField] private float height = 0.55f;
		// 0도는 공격 방향, 양수는 카메라 쪽
		[SerializeField] private float startAngle = -115f;
		[SerializeField] private float endAngle = 60f;
		// 먼 쪽이 높고 가까운 쪽이 낮게 기울인 휘두르기 면
		[SerializeField] private float planeTilt = 22f;
		[SerializeField, Min(1f)] private float easeExponent = 3f;

		[SerializeField, Min(0.01f)] private float swingTime = 0.14f;
		[SerializeField, Min(0f)] private float holdTime = 0.04f;
		[SerializeField, Min(0.01f)] private float fadeTime = 0.08f;
		[SerializeField, Min(0.01f)] private float trailTime = 0.06f;

		// 궤적 안쪽 끝, 칼날 길이 대비 비율
		[SerializeField, Range(0f, 1f)] private float trailInner = 0.6f;
		[SerializeField] private float trailOuter = 1.04f;
		[SerializeField] private Color trailHeadColor = new(0.92f, 0.98f, 1f, 0.9f);
		[SerializeField] private Color trailTailColor = new(0.55f, 0.75f, 1f, 0f);
		[SerializeField, Range(0f, 1f)] private float trailInnerAlpha = 0f;
		[SerializeField, Min(0.1f)] private float trailAlphaCurve = 2f;

		private const int TRAIL_SEGMENTS = 24;

		private readonly Vector3[] trailVertices = new Vector3[(TRAIL_SEGMENTS + 1) * 2];
		private readonly Color[] trailColors = new Color[(TRAIL_SEGMENTS + 1) * 2];
		private Mesh trailMesh;
		private ObjectPoolManager pool;
		private GameObject owner;
		private Transform follow;
		private float elapsed;
		private float side = 1f;

		public float Duration => swingTime + Mathf.Max(holdTime + fadeTime, trailTime);

		private void Awake()
		{
			trailMesh = new Mesh { name = "Sword swing trail" };
			trailMesh.MarkDynamic();
			int[] triangles = new int[TRAIL_SEGMENTS * 6];
			for (int i = 0; i < TRAIL_SEGMENTS; i++)
			{
				int vertex = i * 2;
				int triangle = i * 6;
				triangles[triangle] = vertex;
				triangles[triangle + 1] = vertex + 1;
				triangles[triangle + 2] = vertex + 2;
				triangles[triangle + 3] = vertex + 2;
				triangles[triangle + 4] = vertex + 1;
				triangles[triangle + 5] = vertex + 3;
			}
			trailMesh.vertices = trailVertices;
			trailMesh.colors = trailColors;
			trailMesh.triangles = triangles;
			float reach = handRadius + bladeLength * trailOuter + height;
			trailMesh.bounds = new Bounds(Vector3.zero, Vector3.one * reach * 2);
			trailFilter.sharedMesh = trailMesh;
			trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
			trailRenderer.receiveShadows = false;
			trailRenderer.enabled = false;
			blade.enabled = false;
		}

		public void PlayPooled(ObjectPoolManager objectPool, GameObject attackOwner, Transform followTarget)
		{
			pool = objectPool;
			owner = attackOwner;
			follow = followTarget;
			// 좌우 어느 방향이든 먼 쪽에서 카메라 쪽으로 베도록 회전 부호 결정
			side = Vector3.Dot(transform.right, Vector3.back) >= 0 ? 1f : -1f;
			elapsed = 0;
			Evaluate(0);
		}

		private void LateUpdate()
		{
			if (pool == null)
				return;
			elapsed += Time.deltaTime;
			if (owner == null || owner.activeInHierarchy == false || elapsed >= Duration)
			{
				pool.Despawn(gameObject);
				return;
			}
			if (follow != null)
			{
				Vector3 position = follow.position;
				position.y = transform.position.y;
				transform.position = position;
			}
			Evaluate(elapsed);
		}

		public void Evaluate(float age)
		{
			bool visible = isActiveAndEnabled && age >= 0 && age < Duration;
			blade.enabled = visible;
			trailRenderer.enabled = visible;
			if (visible == false)
				return;

			Quaternion tilt = Quaternion.AngleAxis(-planeTilt * side, Vector3.forward);
			Vector3 planeNormal = tilt * Vector3.up;
			Vector3 pivot = Vector3.up * height;

			Vector3 direction = BladeDirection(age, tilt);
			UpdateBlade(age, direction, planeNormal, pivot);
			UpdateTrail(age, tilt, pivot);
		}

		private Vector3 BladeDirection(float age, Quaternion tilt)
		{
			float progress = Mathf.Clamp01(age / swingTime);
			float eased = 1f - Mathf.Pow(1f - progress, easeExponent);
			float angle = Mathf.Lerp(startAngle, endAngle, eased) * side;
			return tilt * (Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward);
		}

		private void UpdateBlade(float age, Vector3 direction, Vector3 planeNormal, Vector3 pivot)
		{
			Vector3 spriteAxis = new Vector3(spriteTip.x - spriteGrip.x, spriteTip.y - spriteGrip.y, 0);
			float scale = bladeLength / spriteAxis.magnitude;
			// 스프라이트 면을 휘두르기 면에 눕히고 손잡이에서 칼끝 축을 칼날 방향에 맞춤
			Quaternion rotation = Quaternion.LookRotation(planeNormal, direction)
				* Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward, spriteAxis));
			Vector3 grip = pivot + direction * handRadius;
			Transform bladeTransform = blade.transform;
			bladeTransform.localScale = Vector3.one * scale;
			bladeTransform.localRotation = rotation;
			bladeTransform.localPosition = grip - rotation * ((Vector3)spriteGrip * scale);

			float fadeStart = swingTime + holdTime;
			float alpha = 1f - Mathf.Clamp01((age - fadeStart) / fadeTime);
			Color color = blade.color;
			color.a = alpha;
			blade.color = color;
		}

		private void UpdateTrail(float age, Quaternion tilt, Vector3 pivot)
		{
			float head = Mathf.Min(age, swingTime);
			float tail = Mathf.Clamp(age - trailTime, 0f, head);
			float innerRadius = handRadius + bladeLength * trailInner;
			float outerRadius = handRadius + bladeLength * trailOuter;
			// 휘두르기가 끝난 뒤 궤적이 줄어드는 만큼 전체 농도도 낮춤
			float presence = age <= swingTime ? 1f : Mathf.Clamp01((head - tail) / trailTime);
			for (int i = 0; i <= TRAIL_SEGMENTS; i++)
			{
				float along = (float)i / TRAIL_SEGMENTS;
				Vector3 direction = BladeDirection(Mathf.Lerp(tail, head, along), tilt);
				int vertex = i * 2;
				trailVertices[vertex] = pivot + direction * innerRadius;
				trailVertices[vertex + 1] = pivot + direction * outerRadius;

				Color outer = Color.Lerp(trailTailColor, trailHeadColor, along);
				outer.a *= Mathf.Pow(along, trailAlphaCurve) * presence;
				Color inner = outer;
				inner.a *= trailInnerAlpha;
				trailColors[vertex] = inner;
				trailColors[vertex + 1] = outer;
			}
			trailMesh.vertices = trailVertices;
			trailMesh.colors = trailColors;
		}

		private void OnDisable()
		{
			pool = null;
			owner = null;
			follow = null;
			if (blade != null)
				blade.enabled = false;
			if (trailRenderer != null)
				trailRenderer.enabled = false;
		}

		private void OnDestroy()
		{
			if (trailMesh != null)
				Destroy(trailMesh);
		}
	}
}
