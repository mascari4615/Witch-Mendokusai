using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 주기 생성 자리. 정본 memo wm/design/crossing-threat-20261119 th4
	public enum SpawnPlacementPolicy
	{
		// 이동 중이면 진행 방향 앞 부채꼴, 멈추면 둘레. 화면 밖으로 밀지 않음 (기본)
		ForwardCone = 0,
		// 화면 밖 둘레. 이동 중이면 ForwardWeight 확률로 앞 부채꼴
		OffscreenRing = 1,
	}

	// 화면 밖에 오래 있거나 너무 먼 일반 적 처리
	public enum OffscreenRetirePolicy
	{
		// 드롭 없이 반납 (기존)
		Retire = 0,
		// 진행 방향 앞 화면 밖으로 옮김. 뒤에 남은 무리가 앞에서 다시 옴
		Relocate = 1,
	}

	public enum ArenaBoundaryShape
	{
		None = 0,
		Circle = 1,
		Square = 2,
	}

	public enum ArenaBoundaryMode
	{
		// 보이지 않는 벽 (충돌체). 주인공과 적 모두 못 나감
		Wall = 0,
		// 벽 없음. 밖에 있으면 주기적으로 최대 체력 % 피해
		Hazard = 1,
	}

	// 던전 생성과 반납 정책 (th4). 둘 다 기본 기존 동작
	[Serializable]
	public class DungeonThreatPolicy
	{
		// 기본 ForwardCone (앞에서 맞서게, 소환진이 먼저 뜸). OffscreenRing 은 비교 연구용으로 둠, 개발자 창 표시 탭에서 판 중 전환 (2026-11-22)
		[field: SerializeField] public SpawnPlacementPolicy SpawnPolicy { get; private set; } = SpawnPlacementPolicy.ForwardCone;
		[field: SerializeField, Tooltip("앞 부채꼴 반각 (도)"), Range(0f, 180f)] public float ForwardHalfAngle { get; private set; } = 30f;
		[field: SerializeField, Tooltip("OffscreenRing 에서 이동 중 앞 부채꼴을 고를 확률"), Range(0f, 1f)] public float ForwardWeight { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("화면 밖 자리를 찾을 때 거리 증가 폭 (m)"), Min(0.1f)] public float OffscreenStep { get; private set; } = 2f;
		[field: SerializeField, Tooltip("화면 밖 자리를 찾을 최대 거리 (m). 못 찾으면 이 거리. 생성기 반납 거리 (despawnDistance) 보다 짧아야 함"), Min(1f)] public float OffscreenMaxDistance { get; private set; } = 30f;
		[field: SerializeField] public OffscreenRetirePolicy RetirePolicy { get; private set; } = OffscreenRetirePolicy.Retire;

		// 개발자 창 threat. 나머지 값은 원본 그대로 (필드 전체 복사)
		public DungeonThreatPolicy With(SpawnPlacementPolicy spawnPolicy, OffscreenRetirePolicy retirePolicy)
		{
			DungeonThreatPolicy copy = (DungeonThreatPolicy)MemberwiseClone();
			copy.SpawnPolicy = spawnPolicy;
			copy.RetirePolicy = retirePolicy;
			return copy;
		}

		// despawnDistance 는 생성기의 반납 거리. 재배치 자리가 그보다 멀면 다음 틱에 또 옮겨져 매 틱 반복
		public void Validate(string ownerName, float despawnDistance, List<string> errors)
		{
			if (OffscreenMaxDistance >= despawnDistance)
				errors.Add($"{ownerName}: {nameof(OffscreenMaxDistance)} {OffscreenMaxDistance} 이 반납 거리 {despawnDistance} 이상");
		}
	}

	// 유한 경계 맵 (th2). 기본 없음 (무한 지면)
	[Serializable]
	public class DungeonArenaOptions
	{
		public const float MIN_HALF_SIZE = 5f;

		[field: SerializeField] public ArenaBoundaryShape Shape { get; private set; } = ArenaBoundaryShape.None;
		[field: SerializeField] public ArenaBoundaryMode Mode { get; private set; } = ArenaBoundaryMode.Wall;
		[field: SerializeField, Tooltip("판 시작 자리에서 경계까지 (m). 원은 반지름, 사각은 반 변"), Min(MIN_HALF_SIZE)] public float HalfSize { get; private set; } = 40f;
		[field: SerializeField, Tooltip("생성과 맵 오브젝트를 경계 안쪽으로 이만큼 들임 (m)"), Min(0f)] public float SpawnInset { get; private set; } = 1.5f;

		[field: Header("벽")]
		[field: SerializeField, Tooltip("원 경계 벽 조각 수"), Min(8)] public int CircleSegments { get; private set; } = 64;
		[field: SerializeField, Min(0.5f)] public float WallHeight { get; private set; } = 4f;
		[field: SerializeField, Min(0.1f)] public float WallThickness { get; private set; } = 1f;

		[field: Header("밖 피해 (Hazard)")]
		[field: SerializeField, Tooltip("한 번에 최대 체력 %"), Range(0f, 100f)] public float HazardDamagePercent { get; private set; } = 5f;
		[field: SerializeField, Tooltip("피해 간격 (초)"), Min(0.1f)] public float HazardInterval { get; private set; } = 1f;

		[field: Header("바닥 선")]
		[field: SerializeField] public Color LineColor { get; private set; } = new(1f, 0.35f, 0.3f, 1f);
		[field: SerializeField, Min(0.01f)] public float LineWidth { get; private set; } = 0.35f;

		public bool Enabled => Shape != ArenaBoundaryShape.None;

		// 개발자 창 threat arena. 나머지 값은 원본 그대로 (필드 전체 복사)
		public DungeonArenaOptions With(ArenaBoundaryShape shape, ArenaBoundaryMode mode, float halfSize)
		{
			DungeonArenaOptions copy = (DungeonArenaOptions)MemberwiseClone();
			copy.Shape = shape;
			copy.Mode = mode;
			copy.HalfSize = halfSize;
			return copy;
		}

		public void Validate(string ownerName, List<string> errors)
		{
			if (Enabled == false)
				return;
			if (HalfSize < MIN_HALF_SIZE)
				errors.Add($"{ownerName}: 경계 {nameof(HalfSize)} {HalfSize} 이 최소 {MIN_HALF_SIZE} 미만");
			if (SpawnInset >= HalfSize)
				errors.Add($"{ownerName}: 경계 {nameof(SpawnInset)} {SpawnInset} 이 {nameof(HalfSize)} {HalfSize} 이상");
			if (Mode == ArenaBoundaryMode.Hazard && HazardDamagePercent <= 0f)
				errors.Add($"{ownerName}: 밖 피해 경계인데 피해 0");
		}
	}

	// 벽 조각 하나. 경계 중심 기준 수평 위치, 방향 (도, Y 축), 길이
	public readonly struct ArenaWallSegment
	{
		public readonly Vector3 Offset;
		public readonly float Yaw;
		public readonly float Length;

		public ArenaWallSegment(Vector3 offset, float yaw, float length)
		{
			Offset = offset;
			Yaw = yaw;
			Length = length;
		}
	}

	// 엔진 상태 없는 계산. 런타임과 시험이 같은 함수를 씀
	public static class SurvivalThreatRules
	{
		// 이보다 작은 수평 길이 제곱은 멈춤으로 봄
		public const float STILL_SQUARED = 0.0001f;

		// 입력 방향 기준 이동 속도. 방향은 입력 (넉백, 끌림 제외), 크기는 실제 수평 속도. 멈추면 0
		public static Vector3 MoveVelocity(Vector3 moveDirectionWorld, Vector3 velocity)
		{
			moveDirectionWorld.y = 0f;
			if (moveDirectionWorld.sqrMagnitude < STILL_SQUARED)
				return Vector3.zero;
			velocity.y = 0f;
			return moveDirectionWorld.normalized * velocity.magnitude;
		}

		public static Vector3 MoveVelocity(UnitMovement movement) => MoveVelocity(movement.MoveDirectionWorld, movement.Velocity);

		// 진행 방향 앞 부채꼴 안 각도 (재배치). 멈춰 있으면 둘레 절대 각
		public static float PickAheadAngle(bool moving, float forwardHalfAngle, double angleRoll, out bool relativeToForward) =>
			PickSpawnAngle(SpawnPlacementPolicy.ForwardCone, moving, forwardHalfAngle, 1f, 0.0, angleRoll, out relativeToForward);

		// 생성 각도. relativeToForward 면 진행 방향 기준 오프셋 (도), 아니면 월드 절대 각 (도, +X 0 반시계)
		public static float PickSpawnAngle(SpawnPlacementPolicy policy, bool moving, float forwardHalfAngle, float forwardWeight, double coneRoll, double angleRoll, out bool relativeToForward)
		{
			bool useCone = moving && (policy == SpawnPlacementPolicy.ForwardCone || coneRoll < forwardWeight);
			relativeToForward = useCone;
			if (useCone)
				return Mathf.Lerp(-forwardHalfAngle, forwardHalfAngle, (float)angleRoll);
			return (float)angleRoll * 360f;
		}

		public static bool Contains(ArenaBoundaryShape shape, Vector3 center, float halfSize, Vector3 point, float inset)
		{
			float limit = halfSize - inset;
			float x = point.x - center.x;
			float z = point.z - center.z;
			return shape switch
			{
				ArenaBoundaryShape.None => true,
				ArenaBoundaryShape.Circle => x * x + z * z <= limit * limit,
				ArenaBoundaryShape.Square => Mathf.Abs(x) <= limit && Mathf.Abs(z) <= limit,
				_ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
			};
		}

		// 경계 안쪽 (inset 들인 선) 으로 끌어옴. 높이는 그대로
		public static Vector3 Clamp(ArenaBoundaryShape shape, Vector3 center, float halfSize, Vector3 point, float inset)
		{
			if (Contains(shape, center, halfSize, point, inset))
				return point;
			float limit = Mathf.Max(0f, halfSize - inset);
			float x = point.x - center.x;
			float z = point.z - center.z;
			switch (shape)
			{
				case ArenaBoundaryShape.Circle:
					float length = Mathf.Sqrt(x * x + z * z);
					float scale = limit / length;
					return new Vector3(center.x + x * scale, point.y, center.z + z * scale);
				case ArenaBoundaryShape.Square:
					return new Vector3(center.x + Mathf.Clamp(x, -limit, limit), point.y, center.z + Mathf.Clamp(z, -limit, limit));
				default:
					throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
			}
		}

		// 벽 조각 배치. 안쪽 면이 경계선에 오도록 두께 절반만큼 바깥
		public static List<ArenaWallSegment> WallSegments(ArenaBoundaryShape shape, float halfSize, int circleSegments, float thickness)
		{
			List<ArenaWallSegment> segments = new();
			float distance = halfSize + thickness * 0.5f;
			switch (shape)
			{
				case ArenaBoundaryShape.None:
					break;
				case ArenaBoundaryShape.Circle:
					// 조각 끝이 이웃과 겹치게 바깥 둘레 기준 길이에 두께만큼 더함
					float length = 2f * distance * Mathf.Tan(Mathf.PI / circleSegments) + thickness;
					for (int i = 0; i < circleSegments; i++)
					{
						float angle = Mathf.PI * 2f * i / circleSegments;
						Vector3 offset = new(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
						// 조각의 길이 축 (로컬 X) 이 접선. Y 축 회전은 시계 방향이라 -각
						segments.Add(new ArenaWallSegment(offset, 90f - angle * Mathf.Rad2Deg, length));
					}
					break;
				case ArenaBoundaryShape.Square:
					float side = 2f * halfSize + thickness * 2f;
					segments.Add(new ArenaWallSegment(new Vector3(distance, 0f, 0f), 90f, side));
					segments.Add(new ArenaWallSegment(new Vector3(-distance, 0f, 0f), 90f, side));
					segments.Add(new ArenaWallSegment(new Vector3(0f, 0f, distance), 0f, side));
					segments.Add(new ArenaWallSegment(new Vector3(0f, 0f, -distance), 0f, side));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
			}
			return segments;
		}
	}
}
