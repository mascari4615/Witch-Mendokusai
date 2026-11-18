using UnityEngine;

namespace WitchMendokusai
{
	// 바닥에 놓인 부서지는 물건 하나. 주인공 무기에만 맞음 (DamagingObject). 몬스터 목록과 작은 지도에 넣지 않음
	public class BreakableObject : MonoBehaviour, IDamageable
	{
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

		private BreakableField field;
		private Transform meshTransform;
		private MeshFilter meshFilter;
		private MeshRenderer meshRenderer;
		private BoxCollider boxCollider;
		private MaterialPropertyBlock propertyBlock;
		private int health;
		private float hitTime = float.NegativeInfinity;
		private Vector3 hitDirection = Vector3.forward;
		private bool flashing;

		public int KindIndex { get; private set; }
		public int VariantIndex { get; private set; }
		public long Key { get; private set; }
		public Vector2Int Cell { get; private set; }
		public bool IsBroken => health <= 0;
		public Vector3 HitDirection => hitDirection;
		public Bounds LocalBounds => meshFilter.sharedMesh.bounds;

		public static BreakableObject Create(BreakableField field, Transform parent, Material material)
		{
			GameObject root = new("Breakable");
			root.transform.SetParent(parent, false);
			Rigidbody body = root.AddComponent<Rigidbody>();
			body.isKinematic = true;
			body.useGravity = false;
			BoxCollider collider = root.AddComponent<BoxCollider>();
			collider.isTrigger = true;

			GameObject mesh = new("Mesh");
			mesh.transform.SetParent(root.transform, false);
			MeshFilter filter = mesh.AddComponent<MeshFilter>();
			MeshRenderer renderer = mesh.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = material;

			BreakableObject breakable = root.AddComponent<BreakableObject>();
			breakable.field = field;
			breakable.meshTransform = mesh.transform;
			breakable.meshFilter = filter;
			breakable.meshRenderer = renderer;
			breakable.boxCollider = collider;
			breakable.propertyBlock = new MaterialPropertyBlock();
			return breakable;
		}

		public void Place(Vector3 position, float yaw, Vector2Int cell, long key, int kindIndex, int variantIndex, Mesh mesh, int maxHealth)
		{
			transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
			Cell = cell;
			Key = key;
			KindIndex = kindIndex;
			VariantIndex = variantIndex;
			health = maxHealth;
			meshFilter.sharedMesh = mesh;
			boxCollider.center = mesh.bounds.center;
			boxCollider.size = mesh.bounds.size;
			meshTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			hitTime = float.NegativeInfinity;
			ClearFlash();
			// 맞기 전에는 매 프레임 갱신 없음
			enabled = false;
		}

		public void ReceiveDamage(DamageInfo damageInfo)
		{
			if (IsBroken)
				return;
			health -= Mathf.Max(1, damageInfo.damage);
			hitTime = Time.time;
			Vector3 away = damageInfo.damageSource is Component source ? transform.position - source.transform.position : Vector3.zero;
			away.y = 0f;
			Vector2 fallback = Random.insideUnitCircle.normalized;
			hitDirection = away.sqrMagnitude > .0001f ? away.normalized : new Vector3(fallback.x, 0f, fallback.y);
			enabled = true;
			field.OnHit(this);
			if (IsBroken)
				field.Break(this);
		}

		private void Update()
		{
			BreakableSettings settings = field.Settings;
			float elapsed = Time.time - hitTime;
			if (elapsed < settings.ShakeDuration)
			{
				float decay = 1f - elapsed / settings.ShakeDuration;
				float wave = Mathf.Sin(elapsed * settings.ShakeFrequency * Mathf.PI * 2f) * decay;
				Vector3 localDirection = transform.InverseTransformDirection(hitDirection);
				meshTransform.localPosition = localDirection * settings.ShakeDistance * wave;
				Vector3 tiltAxis = Vector3.Cross(Vector3.up, localDirection);
				meshTransform.localRotation = tiltAxis.sqrMagnitude > .0001f
					? Quaternion.AngleAxis(settings.ShakeDegrees * wave, tiltAxis.normalized)
					: Quaternion.identity;
			}
			else if (meshTransform.localPosition != Vector3.zero)
			{
				meshTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}

			if (elapsed < settings.FlashDuration)
			{
				if (flashing == false)
				{
					propertyBlock.SetColor(BASE_COLOR_ID, settings.FlashColor);
					meshRenderer.SetPropertyBlock(propertyBlock);
					flashing = true;
				}
			}
			else if (flashing)
			{
				ClearFlash();
			}

			if (elapsed >= settings.ShakeDuration && elapsed >= settings.FlashDuration)
				enabled = false;
		}

		// 번쩍임이 끝나면 블록을 비워 같은 재질끼리 다시 묶어 그림
		private void ClearFlash()
		{
			flashing = false;
			meshRenderer.SetPropertyBlock(null);
		}
	}
}
