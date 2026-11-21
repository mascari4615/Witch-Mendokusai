using UnityEngine;

namespace WitchMendokusai
{
	// 조준 부품. 마지막 이동 방향, 적 밀집 지점, 회전 발사 각
	public partial class Spawner
	{
		[Header("Move aim")]
		[SerializeField, Min(0f), Tooltip("마지막 이동 방향을 따라 도는 빠르기 (초당). 0 이면 즉시")] private float moveAimFollowRate = 15f;
		[SerializeField, Tooltip("마지막 이동 방향의 반대쪽으로 냄")] private bool reverseAim = false;
		[SerializeField, Min(0f), Tooltip("플레이어 뒤 위치 전략에서 뒤로 떨어질 거리 (월드 단위)")] private float behindDistance = 1f;

		[Header("Dense cluster")]
		[SerializeField, Min(0.1f), Tooltip("한 무리로 보는 반지름 (월드 단위)")] private float clusterRadius = 2.5f;
		[SerializeField, Min(1), Tooltip("무리로 치는 최소 적 수. 이보다 적으면 이번 발은 건너뜀")] private int clusterMinCount = 1;

		[Header("Rotating angle")]
		[SerializeField, Tooltip("발마다 누적으로 더해지는 각도. 조준 방향과 고리 위치가 같이 돎. 0 이면 끔")] private float shotStepDegrees = 0f;
		[SerializeField, Tooltip("한 번 낼 때마다 시작각에 더하는 각도")] private float volleyStepDegrees = 0f;

		private readonly MoveAimTracker moveAim = new();
		private readonly System.Collections.Generic.List<IShotReceiver> shotReceiverBuffer = new();
		private float angleCursor;

		private bool UsesMoveAim => spawnAimStrategy == SpawnAimStrategy.LastMoveDirection || spawnPositionStrategy == SpawnPositionStrategy.BehindPlayer || silhouettePrefab != null;

		private struct ShotPlan
		{
			public bool HasPosition;
			public Vector3 Position;
			public float Angle;
		}

		private void Update()
		{
			if (UsesMoveAim && playerProvider.HasPlayer && playerProvider.HasObject)
				TickMoveAim(Time.deltaTime);
		}

		private void TickMoveAim(float deltaTime)
		{
			moveAim.Tick(playerProvider.CurrentObject.UnitMovement.LookDirection, moveAimFollowRate, deltaTime);
		}

		private bool TryGetDensePoint(Vector3 origin, out Vector3 point)
			=> DensityTargeting.TryFindDensestMonsters(origin, targetRange, clusterRadius, clusterMinCount, out point, out _);

		private void AdvanceAngle(int shotCount) => angleCursor = Mathf.Repeat(angleCursor + shotStepDegrees * shotCount + volleyStepDegrees, 360f);

		// 이번 번의 자리를 미리 정함. 발 i 의 각도는 시작각 + 발 간격 x i
		private ShotPlan[] PlanVolley()
		{
			transform.position = playerProvider.Current.transform.position;
			int count = SpawnCount;
			float baseAngle = angleCursor;
			AdvanceAngle(count);
			ShotPlan[] plan = new ShotPlan[count];
			for (int i = 0; i < count; i++)
			{
				float angle = baseAngle + shotStepDegrees * i;
				plan[i].Angle = angle;
				plan[i].HasPosition = TryGetSpawnPosition(i, count, angle, out plan[i].Position);
			}
			return plan;
		}
	}
}
