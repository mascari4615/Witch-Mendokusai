using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 공격 방향 기준 수평면에서 잰 실제 판정 범위
	// 판정 모양과 수치가 바뀌어도 연출은 이 값만 읽음
	public readonly struct SwordSwingArea
	{
		// 좌우 각도 범위를 고르게 나눈 거리 측정 지점 수
		public const int REACH_SAMPLES = 33;

		public readonly float InnerRadius;
		public readonly float OuterRadius;
		// 공격 방향 좌우로 판정이 닿는 각도
		public readonly float HalfAngle;
		// -HalfAngle부터 HalfAngle까지 각도별로 판정 끝까지의 거리, 없으면 모든 각도에서 OuterRadius
		private readonly float[] reach;

		// 공격마다 측정하므로 메인 스레드 전용 버퍼 재사용
		private static readonly List<Collider> ColliderBuffer = new();
		private static readonly Vector3[] CornerBuffer = new Vector3[8];
		private static readonly Vector2[] PointBuffer = new Vector2[16];
		private static readonly Vector2[] HullBuffer = new Vector2[33];

		public SwordSwingArea(float innerRadius, float outerRadius, float halfAngle) : this(innerRadius, outerRadius, halfAngle, null)
		{
		}

		private SwordSwingArea(float innerRadius, float outerRadius, float halfAngle, float[] reach)
		{
			InnerRadius = Mathf.Max(0f, innerRadius);
			OuterRadius = Mathf.Max(InnerRadius, outerRadius);
			HalfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
			this.reach = reach;
		}

		// 공격 방향 기준 각도에서 판정 끝까지의 거리, 판정 밖 각도는 0
		public float ReachAt(float angle)
		{
			if (reach == null)
				return OuterRadius;
			if (Mathf.Abs(angle) > HalfAngle)
				return 0f;
			if (HalfAngle <= 0f)
				return reach[REACH_SAMPLES / 2];
			float position = (angle + HalfAngle) / (HalfAngle * 2f) * (REACH_SAMPLES - 1);
			int index = Mathf.Min(REACH_SAMPLES - 2, Mathf.FloorToInt(position));
			float near = reach[index];
			float far = reach[index + 1];
			if (near <= 0f || far <= 0f)
				return Mathf.Lerp(near, far, position - index);
			// 이웃 측정점을 잇는 선분과 만나는 거리, 판정의 곧은 면은 측정 간격과 무관하게 곧게 유지
			Vector2 start = Planar(SampleAngle(index)) * near;
			Vector2 edge = Planar(SampleAngle(index + 1)) * far - start;
			Vector2 direction = Planar(angle);
			float denominator = Cross(direction, edge);
			if (Mathf.Abs(denominator) < 1e-6f)
				return Mathf.Lerp(near, far, position - index);
			return Cross(start, edge) / denominator;
		}

		private float SampleAngle(int sample) => Mathf.Lerp(-HalfAngle, HalfAngle, (float)sample / (REACH_SAMPLES - 1));

		private static Vector2 Planar(float angle) => new(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));

		// 연출이 끝날 때까지 판정 측정 버퍼가 덮여도 유지되도록 각도별 거리를 복사
		public SwordSwingArea CopyReachTo(float[] buffer)
		{
			if (reach == null)
				return this;
			System.Array.Copy(reach, buffer, REACH_SAMPLES);
			return new SwordSwingArea(InnerRadius, OuterRadius, HalfAngle, buffer);
		}

		// 판정 오브젝트의 모든 판정과 수명 동안의 이동을 공격 기준 좌표로 측정
		// reachBuffer를 넘기면 각도별 거리를 그 버퍼에 채움, 없으면 새로 할당
		public static SwordSwingArea FromHitbox(Transform hitboxRoot, float[] reachBuffer = null)
		{
			// 날아가는 판정은 수명 동안 나아간 자리까지 쓸고 지나간 범위로 측정
			Vector2 travel = Vector2.zero;
			if (hitboxRoot.TryGetComponent(out BulletMovement movement) && hitboxRoot.TryGetComponent(out ObjectWithTime lifetime))
			{
				Vector3 localTravel = hitboxRoot.InverseTransformDirection(movement.MoveDirection) * (movement.MoveSpeed * lifetime.Duration);
				travel = new Vector2(localTravel.x, localTravel.z);
			}
			int pointsPerCollider = travel == Vector2.zero ? 8 : 16;

			float nearest = float.MaxValue;
			float farthest = 0f;
			float halfAngle = 0f;
			hitboxRoot.GetComponentsInChildren(true, ColliderBuffer);
			foreach (Collider collider in ColliderBuffer)
			{
				CollectCorners(collider, CornerBuffer);
				for (int i = 0; i < CornerBuffer.Length; i++)
				{
					Vector3 local = hitboxRoot.InverseTransformPoint(CornerBuffer[i]);
					PointBuffer[i] = new Vector2(local.x, local.z);
					PointBuffer[i + 8] = PointBuffer[i] + travel;
					nearest = Mathf.Min(nearest, Mathf.Max(0f, local.z));
				}
				for (int i = 0; i < pointsPerCollider; i++)
				{
					Vector2 planar = PointBuffer[i];
					farthest = Mathf.Max(farthest, planar.magnitude);
					halfAngle = Mathf.Max(halfAngle, Mathf.Atan2(Mathf.Abs(planar.x), Mathf.Max(planar.y, 0.01f)) * Mathf.Rad2Deg);
				}
			}
			if (farthest <= 0f)
			{
				ColliderBuffer.Clear();
				return new SwordSwingArea(0f, 0f, 0f);
			}

			float[] reach = reachBuffer ?? new float[REACH_SAMPLES];
			System.Array.Clear(reach, 0, REACH_SAMPLES);
			foreach (Collider collider in ColliderBuffer)
			{
				CollectCorners(collider, CornerBuffer);
				for (int i = 0; i < CornerBuffer.Length; i++)
				{
					Vector3 local = hitboxRoot.InverseTransformPoint(CornerBuffer[i]);
					PointBuffer[i] = new Vector2(local.x, local.z);
					PointBuffer[i + 8] = PointBuffer[i] + travel;
				}
				int hullCount = BuildHull(PointBuffer, pointsPerCollider, HullBuffer);
				for (int sample = 0; sample < REACH_SAMPLES; sample++)
				{
					Vector2 direction = Planar(Mathf.Lerp(-halfAngle, halfAngle, (float)sample / (REACH_SAMPLES - 1)));
					reach[sample] = Mathf.Max(reach[sample], RayExit(direction, HullBuffer, hullCount));
				}
			}
			ColliderBuffer.Clear();
			return new SwordSwingArea(nearest, farthest, halfAngle, reach);
		}

		// 원점에서 direction으로 나간 반직선이 볼록 다각형을 빠져나가는 거리, 만나지 않으면 0
		private static float RayExit(Vector2 direction, Vector2[] hull, int count)
		{
			float exit = 0f;
			for (int i = 0; i < count; i++)
			{
				Vector2 start = hull[i];
				Vector2 edge = hull[(i + 1) % count] - start;
				float denominator = Cross(direction, edge);
				if (Mathf.Abs(denominator) < 1e-6f)
					continue;
				float distance = Cross(start, edge) / denominator;
				float along = Cross(start, direction) / denominator;
				if (distance > 0f && along >= -1e-4f && along <= 1f + 1e-4f)
					exit = Mathf.Max(exit, distance);
			}
			return exit;
		}

		private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

		// 점들의 볼록 껍질을 반시계 순서로 hull에 채우고 꼭짓점 수 반환
		private static int BuildHull(Vector2[] points, int count, Vector2[] hull)
		{
			System.Array.Sort(points, 0, count, PointComparer.Instance);
			int size = 0;
			for (int i = 0; i < count; i++)
			{
				while (size >= 2 && Cross(hull[size - 1] - hull[size - 2], points[i] - hull[size - 2]) <= 0f)
					size--;
				hull[size++] = points[i];
			}
			int lower = size + 1;
			for (int i = count - 2; i >= 0; i--)
			{
				while (size >= lower && Cross(hull[size - 1] - hull[size - 2], points[i] - hull[size - 2]) <= 0f)
					size--;
				hull[size++] = points[i];
			}
			return Mathf.Max(1, size - 1);
		}

		private sealed class PointComparer : IComparer<Vector2>
		{
			public static readonly PointComparer Instance = new();

			public int Compare(Vector2 a, Vector2 b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
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
