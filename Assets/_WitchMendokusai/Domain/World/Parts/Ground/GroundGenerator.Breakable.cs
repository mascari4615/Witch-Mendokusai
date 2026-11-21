using UnityEngine;

namespace WitchMendokusai
{
	// 생존 던전 바닥의 부서지는 물건 칸 배치. 규칙과 수치는 Resources/BreakableSettings
	public partial class GroundGenerator
	{
		private BreakableField breakableField;
		private bool breakableRunActive;

		public BreakableField Breakables => breakableField;
		// 이번 판 정리가 끝나 고정 배치를 받을 수 있는 상태
		public bool BreakablesReady => breakableField != null && breakableRunActive;

		// 켜면 칸 배치를 멈추고 고정 배치만 둠. 개발자 진열장이 켜고 끔
		public static bool SuppressScatter { get; set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetSuppressScatter() => SuppressScatter = false;

		private void Update()
		{
			if (playerProvider.CurrentObject == null)
				return;
			// 월드가 내려가며 부모와 물건이 함께 파괴된 묶음은 버리고 다음 판에 새로 만듦
			if (breakableField != null && breakableField.IsRootLost)
				breakableField = null;
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
			if (SuppressScatter)
			{
				breakableField.EnsureReady();
				breakableField.TickShards();
				return;
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
