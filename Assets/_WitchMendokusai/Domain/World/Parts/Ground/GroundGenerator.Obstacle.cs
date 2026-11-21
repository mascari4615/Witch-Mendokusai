using UnityEngine;

namespace WitchMendokusai
{
	// 던전이 연결한 GroundObstacleSettings 대로 막는 지형 장애물을 둠. 연결이 없으면 장애물 없는 맵
	public partial class GroundGenerator
	{
		private GroundObstacleField obstacleField;
		private GroundObstacleSettings obstacleSettings;
		private int obstacleRunVersion = -1;

		// 지금 판의 장애물. 몹 생성 자리 보정이 씀. 장애물 없는 판은 null
		public static GroundObstacleField ActiveObstacles { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetActiveObstacles() => ActiveObstacles = null;

		private void TickObstacles(bool inDungeon)
		{
			GroundObstacleSettings wanted = null;
			if (inDungeon && SuppressScatter == false && DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager) && dungeonManager.CurDungeon != null)
				wanted = dungeonManager.CurDungeon.GroundObstacles;
			if (wanted == null)
			{
				ReleaseObstacles();
				return;
			}
			obstacleField ??= new GroundObstacleField(transform);
			Vector3 playerPosition = playerProvider.CurrentObject.UnitMovement.Position;
			if (obstacleSettings != wanted || obstacleRunVersion != RunSeed.Version)
			{
				obstacleSettings = wanted;
				obstacleRunVersion = RunSeed.Version;
				obstacleField.Reset(wanted.Rules, RunSeed.Derive(wanted.Rules.SeedSalt), playerPosition);
			}
			ActiveObstacles = obstacleField;
			obstacleField.Tick(playerPosition);
		}

		private void ReleaseObstacles()
		{
			obstacleField?.ReleaseAll();
			obstacleSettings = null;
			obstacleRunVersion = -1;
			if (ActiveObstacles == obstacleField)
				ActiveObstacles = null;
		}

		private void DestroyObstacles()
		{
			ReleaseObstacles();
			obstacleField?.Dispose();
			obstacleField = null;
		}
	}
}
