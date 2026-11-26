using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 탄 하나가 나가는 쪽과 빠르기 (수평)
	public struct VolleyShot
	{
		public Vector3 Direction;
		public float Speed;
	}

	// 탄막 한 물결의 배치 계산. 상태 없음, 시험이 직접 부름. 정본 memo wm/design/boss-volley-20261126
	public static class VolleyPatternMath
	{
		// 수평면 각도 (도, +Z 에서 시계 방향) 의 방향
		public static Vector3 DirectionOf(float degrees) => Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;

		public static float AngleOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

		// 한 물결에 나가는 탄 수
		public static int ShotCount(VolleyShape shape, int count, int perSpoke) => shape == VolleyShape.Spokes ? count * perSpoke : count;

		// 같은 물결 안 탄 차례 (index) 의 빠르기. 뒤 탄일수록 speedStep 만큼 느림. 방사줄은 갈래 안 차례
		public static float SpeedOf(float speed, float speedStep, int order) => speed - speedStep * order;

		// waveIndex 번째 물결까지 돌아간 각도. reverseAtWave 물결 뒤로는 반대로 돎 (0 이면 안 꺾음)
		public static float TurnOffset(int waveIndex, float turnPerWave, int reverseAtWave)
		{
			if (reverseAtWave <= 0 || waveIndex <= reverseAtWave)
				return waveIndex * turnPerWave;
			return (2 * reverseAtWave - waveIndex) * turnPerWave;
		}

		// waveIndex 번째 물결의 탄들. aimDegrees 는 준비 때 고정한 주인공 쪽
		public static void Wave(VolleyShape shape, int count, float arcDegrees, int perSpoke, float aimDegrees, int waveIndex, float turnPerWave,
			float speed, float speedStep, List<VolleyShot> results, int reverseAtWave = 0)
		{
			results.Clear();
			float start = aimDegrees + TurnOffset(waveIndex, turnPerWave, reverseAtWave);
			switch (shape)
			{
				case VolleyShape.Fan:
					for (int i = 0; i < count; i++)
					{
						float offset = count > 1 ? -arcDegrees * 0.5f + arcDegrees * i / (count - 1) : 0f;
						results.Add(new VolleyShot { Direction = DirectionOf(start + offset), Speed = SpeedOf(speed, speedStep, i) });
					}
					break;
				case VolleyShape.Spokes:
					for (int i = 0; i < count; i++)
					{
						Vector3 direction = DirectionOf(start + 360f * i / count);
						for (int j = 0; j < perSpoke; j++)
							results.Add(new VolleyShot { Direction = direction, Speed = SpeedOf(speed, speedStep, j) });
					}
					break;
				default:
					// 고리, 나선, 분열의 작은 탄 고리: 온 둘레를 고르게
					for (int i = 0; i < count; i++)
						results.Add(new VolleyShot { Direction = DirectionOf(start + 360f * i / count), Speed = SpeedOf(speed, speedStep, i) });
					break;
			}
		}

		// 수평면에서 점 point 와 선분 (from, to) 사이 가장 가까운 거리
		public static float SegmentDistance(Vector3 from, Vector3 to, Vector3 point)
		{
			Vector2 start = new(from.x, from.z);
			Vector2 segment = new Vector2(to.x, to.z) - start;
			Vector2 offset = new Vector2(point.x, point.z) - start;
			float lengthSquared = segment.sqrMagnitude;
			float along = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(offset, segment) / lengthSquared) : 0f;
			return (offset - segment * along).magnitude;
		}

		// 이웃한 두 갈래 사이 각도 (도). 부채는 살 사이, 나머지는 온 둘레를 나눈 값
		public static float NeighborDegrees(VolleyShape shape, int count, float arcDegrees) =>
			shape == VolleyShape.Fan ? (count > 1 ? arcDegrees / (count - 1) : 360f) : 360f / Mathf.Max(1, count);

		// 쏜 자리에서 distance 만큼 나간 이웃한 두 탄의 가장자리 사이 틈 (m). 몸이 지나가려면 몸 지름보다 넓어야 함
		public static float GapAt(float neighborDegrees, float distance, float bulletRadius) =>
			2f * distance * Mathf.Sin(neighborDegrees * 0.5f * Mathf.Deg2Rad) - 2f * bulletRadius;
	}
}
