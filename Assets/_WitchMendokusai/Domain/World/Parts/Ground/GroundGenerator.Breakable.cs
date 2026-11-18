using UnityEngine;

namespace WitchMendokusai
{
	// 생존 던전 바닥의 부서지는 물건 칸 배치. 규칙과 수치는 Resources/BreakableSettings
	public partial class GroundGenerator
	{
		private BreakableField breakableField;
		private bool breakableRunActive;

		public BreakableField Breakables => breakableField;

		private void Update()
		{
			if (playerProvider.CurrentObject == null)
				return;
			bool inDungeon = DungeonManager.TryGetExistingInstance(out DungeonManager dungeonManager) && dungeonManager.IsDungeon;
			if (inDungeon == false)
			{
				// 결과 화면에서도 날던 조각은 마저 떨어짐
				breakableRunActive = false;
				breakableField?.TickShards();
				return;
			}
			breakableField ??= new BreakableField(objectPoolManager, gameLogic, ResourceManager.Instance);
			// 재도전은 스테이지를 다시 켜지 않으므로 판이 새로 시작하는 순간 비움
			if (breakableRunActive == false)
			{
				breakableField.ReleaseAll();
				breakableRunActive = true;
			}
			breakableField.Tick(playerProvider.CurrentObject.UnitMovement.Position);
		}

		private void ReleaseBreakables() => breakableField?.ReleaseAll();

		private void DestroyBreakables()
		{
			breakableField?.Dispose();
			breakableField = null;
		}
	}
}
