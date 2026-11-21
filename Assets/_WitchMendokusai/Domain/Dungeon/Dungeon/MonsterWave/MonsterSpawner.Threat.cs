using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 횡단 위협 대응. 주기 생성 자리와 화면 밖 적 재배치 (th4), 유한 경계 (th2)
	// 정책은 던전 자산 값으로 시작하고, 개발자 창 threat 로 판 중에 바꿔 비교
	// 정본 memo wm/design/crossing-threat-20261119, memo wm/design/map-objects-20261119/decisions.md 3단계
	public partial class MonsterSpawner
	{
		[SerializeField, Min(0f), Tooltip("장애물 밖으로 밀어낼 때 쓰는 몹 몸 반지름 (m)")] private float spawnBodyRadius = .5f;
		private DungeonThreatPolicy threatPolicy = new();
		private DungeonArenaOptions arena = new();
		private int visibilityPlanesFrame = -1;
		private bool visibilityPlanesValid;
		private Transform arenaRoot;
		private Vector3 arenaCenter;
		// 판 시작 자리가 정해진 뒤 (첫 판 틱) 경계를 지음
		private bool arenaPending;
		private float nextHazardTime;

		public DungeonThreatPolicy ThreatPolicy => threatPolicy;
		public DungeonArenaOptions Arena => arena;
		public bool HasArena => arenaRoot != null;
		public Vector3 ArenaCenter => arenaCenter;

		private void InitThreat(Dungeon dungeon)
		{
			StopThreat();
			SetThreatPolicy(dungeon.ThreatPolicy);
			arena = dungeon.Arena;
			arenaPending = arena.Enabled;
		}

		private void StopThreat()
		{
			DestroyArena();
			arenaPending = false;
		}

		// 개발자 창 threat. 다음 생성과 다음 반납 판정부터
		public void SetThreatPolicy(DungeonThreatPolicy policy)
		{
			List<string> errors = new();
			policy.Validate(nameof(DungeonThreatPolicy), despawnDistance, errors);
			if (errors.Count > 0)
				throw new System.InvalidOperationException(string.Join(", ", errors));
			threatPolicy = policy;
		}

		// 개발자 창 threat arena. 지금 주인공 자리를 중심으로 다시 지음
		public void SetArena(DungeonArenaOptions options)
		{
			List<string> errors = new();
			options.Validate(nameof(SetArena), errors);
			if (errors.Count > 0)
				throw new System.InvalidOperationException(string.Join(", ", errors));
			DestroyArena();
			arena = options;
			arenaPending = arena.Enabled;
		}

		private void UpdateThreat()
		{
			EnsureArena();
			UpdateArenaHazard();
		}

		// 판 틱과 맵 오브젝트 첫 배치 중 앞선 쪽이 지음. 맵 오브젝트 자리 검사에 경계 반영
		private void EnsureArena()
		{
			if (arenaPending == false)
				return;
			arenaPending = false;
			BuildArena(playerProvider.Current.transform.position);
		}

		private void BuildArena(Vector3 center)
		{
			arenaCenter = center;
			arenaRoot = new GameObject("ArenaBoundary").transform;
			arenaRoot.position = center;
			MapObjectSettings settings = MapObjectSettings.Load();
			LineRenderer line = GroundLine.Make(arenaRoot, "Line", settings.MarkerMaterial, arena.LineColor, arena.LineWidth);
			line.transform.localPosition = Vector3.up * settings.GroundLift;
			if (arena.Shape == ArenaBoundaryShape.Circle)
				GroundLine.SetPolygon(line, arena.CircleSegments, arena.HalfSize, 0f);
			else
				GroundLine.SetPolygon(line, 4, arena.HalfSize * Mathf.Sqrt(2f), Mathf.PI * 0.25f);
			nextHazardTime = ElapsedSeconds + arena.HazardInterval;
			if (arena.Mode == ArenaBoundaryMode.Wall)
			{
				foreach (ArenaWallSegment segment in SurvivalThreatRules.WallSegments(arena.Shape, arena.HalfSize, arena.CircleSegments, arena.WallThickness))
				{
					GameObject wall = new("Wall");
					wall.transform.SetParent(arenaRoot, false);
					wall.transform.localPosition = segment.Offset + Vector3.up * (arena.WallHeight * 0.5f);
					wall.transform.localRotation = Quaternion.Euler(0f, segment.Yaw, 0f);
					BoxCollider box = wall.AddComponent<BoxCollider>();
					box.size = new Vector3(segment.Length, arena.WallHeight, arena.WallThickness);
				}
				// 같은 틱의 이동 계산이 새 벽을 보게
				Physics.SyncTransforms();
			}
			Debug.Log($"[Threat] 경계 {arena.Shape} {arena.Mode} 반경 {arena.HalfSize}m, 중심 {center}");
		}

		private void DestroyArena()
		{
			if (arenaRoot == null)
				return;
			arenaRoot.gameObject.SetActive(false);
			Destroy(arenaRoot.gameObject);
			arenaRoot = null;
		}

		// 밖 피해 경계. 판 시간 기준 간격마다 최대 체력 %
		private void UpdateArenaHazard()
		{
			if (HasArena == false || arena.Mode != ArenaBoundaryMode.Hazard)
				return;
			float elapsed = ElapsedSeconds;
			if (elapsed < nextHazardTime)
				return;
			nextHazardTime = elapsed + arena.HazardInterval;
			PlayerObject player = playerProvider.CurrentObject;
			if (player.IsAlive == false || IsInsideArena(player.transform.position, 0f))
				return;
			player.ReceiveDamage(new DamageInfo
			{
				type = DamageType.Normal,
				damage = MapObjectRules.PercentOfMaxHealth(player.UnitStat[UnitStatType.HP_MAX], arena.HazardDamagePercent),
				damageSource = null,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
			});
		}

		public bool IsInsideArena(Vector3 position, float inset) =>
			HasArena == false || SurvivalThreatRules.Contains(arena.Shape, arenaCenter, arena.HalfSize, position, inset);

		// 생성 자리를 경계 안쪽으로. 경계가 없으면 그대로. 벽 쪽으로 달리는 중이면 끌어온 자리가 화면 안일 수 있음 (경계 판의 한계)
		public Vector3 ClampToArena(Vector3 position) =>
			ClearOfObstacles(HasArena ? SurvivalThreatRules.Clamp(arena.Shape, arenaCenter, arena.HalfSize, position, arena.SpawnInset) : position);

		// 막는 지형 장애물 속에 생성되지 않게 가장자리 밖으로. 장애물 없는 판은 그대로
		private Vector3 ClearOfObstacles(Vector3 position) =>
			GroundGenerator.ActiveObstacles != null ? GroundGenerator.ActiveObstacles.PushOut(position, spawnBodyRadius) : position;

		// 주기 웨이브 한 마리 자리. 정책에 따라 앞 부채꼴 또는 화면 밖 둘레
		private Vector3 PickPeriodicSpawnPosition()
		{
			Vector3 playerPosition = playerProvider.Current.transform.position;
			// 입력값 (MoveDirectionLocal) 은 카메라 기준 축. 생성 방향은 월드 이동 방향
			Vector3 forward = playerProvider.CurrentObject.UnitMovement.MoveDirectionWorld;
			forward.y = 0f;
			bool moving = forward.sqrMagnitude > SurvivalThreatRules.STILL_SQUARED;
			float angle = SurvivalThreatRules.PickSpawnAngle(threatPolicy.SpawnPolicy, moving, threatPolicy.ForwardHalfAngle, threatPolicy.ForwardWeight, Random.value, Random.value, out bool relativeToForward);
			Vector3 direction = SpawnDirection(forward, angle, relativeToForward);
			float distance = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
			if (threatPolicy.SpawnPolicy == SpawnPlacementPolicy.OffscreenRing)
				distance = PushOffscreen(playerPosition, direction, distance);
			return ClampToArena(playerPosition + direction * distance);
		}

		// 화면 밖에 오래 있던 적을 진행 방향 앞 화면 밖으로. 멈춰 있으면 둘레 아무 데나 화면 밖
		private Vector3 PickRelocation()
		{
			Vector3 playerPosition = playerProvider.Current.transform.position;
			Vector3 forward = playerProvider.CurrentObject.UnitMovement.MoveDirectionWorld;
			forward.y = 0f;
			bool moving = forward.sqrMagnitude > SurvivalThreatRules.STILL_SQUARED;
			float angle = SurvivalThreatRules.PickAheadAngle(moving, threatPolicy.ForwardHalfAngle, Random.value, out bool relativeToForward);
			Vector3 direction = SpawnDirection(forward, angle, relativeToForward);
			float distance = PushOffscreen(playerPosition, direction, spawnDistanceRange.y);
			return ClampToArena(playerPosition + direction * distance);
		}

		private static Vector3 SpawnDirection(Vector3 forward, float angle, bool relativeToForward)
		{
			if (relativeToForward)
				return Quaternion.Euler(0f, angle, 0f) * forward.normalized;
			float radians = angle * Mathf.Deg2Rad;
			return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
		}

		// 화면 밖이 될 때까지 거리를 늘림. 상한까지 못 찾으면 상한 거리. 카메라가 없으면 (전환 중) 처음 거리
		private float PushOffscreen(Vector3 origin, Vector3 direction, float distance)
		{
			if (RefreshVisibilityPlanes() == false)
				return distance;
			while (distance < threatPolicy.OffscreenMaxDistance)
			{
				if (IsOnScreen(PaddedBounds(origin + direction * distance)) == false)
					return distance;
				distance += threatPolicy.OffscreenStep;
			}
			return threatPolicy.OffscreenMaxDistance;
		}

		// 화면 밖 판정 한 벌. 반납 (RetireDistantMonsters) 과 생성 자리가 같은 평면과 여유 (visibilityPadding) 를 씀. 한 프레임에 한 번 계산
		private bool RefreshVisibilityPlanes()
		{
			if (visibilityPlanesFrame == Time.frameCount)
				return visibilityPlanesValid;
			visibilityPlanesFrame = Time.frameCount;
			Camera camera = Camera.main;
			visibilityPlanesValid = camera != null;
			if (visibilityPlanesValid)
				GeometryUtility.CalculateFrustumPlanes(camera, visibilityPlanes);
			return visibilityPlanesValid;
		}

		private bool IsOnScreen(Bounds bounds)
		{
			bounds.Expand(visibilityPadding * 2f);
			return GeometryUtility.TestPlanesAABB(visibilityPlanes, bounds);
		}

		// 그림 크기를 모르는 빈 자리. 발밑에서 몸 높이 정도
		private Bounds PaddedBounds(Vector3 position) => new(position + Vector3.up * visibilityPadding, Vector3.zero);

		// 반납 대상 일반 적. Relocate 면 진행 중인 공격을 끊고 앞 화면 밖으로 옮김, 아니면 끔
		// 패턴 이동 중인 적 (벽, 포위) 은 옮기면 대형이 깨져서 정책과 무관하게 끔
		private void RetireOrRelocate(GameObject obj, MonsterObject monster)
		{
			bool patterned = obj.TryGetComponent(out WaveMoveOverride moveOverride) && moveOverride.enabled;
			if (threatPolicy.RetirePolicy == OffscreenRetirePolicy.Relocate && patterned == false)
			{
				if (obj.TryGetComponent(out FSMSlime brain))
					brain.InterruptAttack();
				monster.UnitMovement.Teleport(PickRelocation());
				monster.OffscreenSinceTime = -1f;
				return;
			}
			obj.SetActive(false);
		}
	}
}
