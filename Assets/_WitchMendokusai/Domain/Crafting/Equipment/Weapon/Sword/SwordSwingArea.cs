using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 공격 방향 기준 수평면에서 잰 실제 판정 범위, 부채꼴 값과 공격 좌표의 상자 범위
	// 판정 모양과 수치가 바뀌어도 연출은 이 값만 읽음
	public readonly struct SwordSwingArea
	{
		public readonly float InnerRadius;
		public readonly float OuterRadius;
		// 공격 방향 좌우로 판정이 닿는 각도
		public readonly float HalfAngle;
		// 공격 좌표(x 오른쪽, z 정면)에서 판정을 감싸는 상자, 폭이 0이면 상자 정보 없음
		public readonly float MinX;
		public readonly float MaxX;
		public readonly float MinZ;
		public readonly float MaxZ;

		public float Width => MaxX - MinX;
		public float CenterX => (MinX + MaxX) * 0.5f;
		public float Depth => MaxZ - MinZ;

		// 공격마다 측정하므로 메인 스레드 전용 버퍼 재사용
		private static readonly List<Collider> ColliderBuffer = new();
		private static readonly Vector3[] CornerBuffer = new Vector3[8];

		public SwordSwingArea(float innerRadius, float outerRadius, float halfAngle)
		{
			InnerRadius = Mathf.Max(0f, innerRadius);
			OuterRadius = Mathf.Max(InnerRadius, outerRadius);
			HalfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
			MinX = 0f;
			MaxX = 0f;
			MinZ = InnerRadius;
			MaxZ = OuterRadius;
		}

		public SwordSwingArea(float innerRadius, float outerRadius, float halfAngle, Vector2 min, Vector2 max) : this(innerRadius, outerRadius, halfAngle)
		{
			MinX = min.x;
			MaxX = Mathf.Max(min.x, max.x);
			MinZ = min.y;
			MaxZ = Mathf.Max(min.y, max.y);
		}

		// 판정 오브젝트의 모든 판정과 수명 동안의 이동을 공격 기준 좌표로 측정
		public static SwordSwingArea FromHitbox(Transform hitboxRoot)
		{
			float nearest = float.MaxValue;
			float farthest = 0f;
			float halfAngle = 0f;
			Vector2 min = new(float.MaxValue, float.MaxValue);
			Vector2 max = new(float.MinValue, float.MinValue);
			hitboxRoot.GetComponentsInChildren(true, ColliderBuffer);
			foreach (Collider collider in ColliderBuffer)
			{
				CollectCorners(collider, CornerBuffer);
				for (int i = 0; i < CornerBuffer.Length; i++)
				{
					Vector3 local = hitboxRoot.InverseTransformPoint(CornerBuffer[i]);
					Vector2 planar = new(local.x, local.z);
					nearest = Mathf.Min(nearest, Mathf.Max(0f, local.z));
					farthest = Mathf.Max(farthest, planar.magnitude);
					halfAngle = Mathf.Max(halfAngle, Mathf.Atan2(Mathf.Abs(local.x), Mathf.Max(local.z, 0.01f)) * Mathf.Rad2Deg);
					min = Vector2.Min(min, planar);
					max = Vector2.Max(max, planar);
				}
			}
			ColliderBuffer.Clear();
			if (farthest <= 0f)
				return new SwordSwingArea(0f, 0f, 0f);

			// 날아가는 판정은 수명 동안 공격 방향으로 나아간 거리만큼 범위 연장
			if (hitboxRoot.TryGetComponent(out BulletMovement movement) && hitboxRoot.TryGetComponent(out ObjectWithTime lifetime))
			{
				float travel = movement.MoveSpeed * lifetime.Duration * Vector3.Dot(movement.MoveDirection, hitboxRoot.forward);
				farthest += Mathf.Max(0f, travel);
				max.y += Mathf.Max(0f, travel);
			}
			return new SwordSwingArea(nearest, farthest, halfAngle, min, max);
		}

		private static void CollectCorners(Collider collider, Vector3[] corners)
		{
			Transform owner = collider.transform;
			if (collider is BoxCollider box)
			{
				Vector3 half = box.size * 0.5f;
				for (int i = 0; i < 8; i++)
				{
					Vector3 sign = new((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
					corners[i] = owner.TransformPoint(box.center + Vector3.Scale(half, sign));
				}
				return;
			}
			// 상자 외 판정은 월드 경계 상자의 꼭짓점으로 근사
			Bounds bounds = collider.bounds;
			for (int i = 0; i < 8; i++)
			{
				Vector3 sign = new((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
				corners[i] = bounds.center + Vector3.Scale(bounds.extents, sign);
			}
		}
	}
}
