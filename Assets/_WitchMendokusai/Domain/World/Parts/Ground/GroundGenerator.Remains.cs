using UnityEngine;

namespace WitchMendokusai
{
	// 생존 던전 바닥에 남는 것 (잔해, 자국, 액체). 부서지는 물건과 같은 수명: 판 시작에 비우고 결과 화면에선 날던 것만
	public partial class GroundGenerator
	{
		private RemainsWorld remainsWorld;
		private bool remainsRunActive;

		private void TickRemains(bool inDungeon)
		{
			if (remainsWorld != null && remainsWorld.IsRootLost)
			{
				remainsWorld.Dispose();
				remainsWorld = null;
			}
			if (inDungeon == false)
			{
				remainsRunActive = false;
				remainsWorld?.TickIdle(Time.deltaTime);
				return;
			}
			if (remainsWorld == null)
			{
				// 바닥 높이는 부서지는 물건과 같은 값 (같은 바닥)
				remainsWorld = new RemainsWorld { GroundHeight = Resources.Load<BreakableSettings>(nameof(BreakableSettings)).GroundHeight };
			}
			if (remainsRunActive == false)
			{
				remainsWorld.ReleaseAll();
				remainsRunActive = true;
			}
			remainsWorld.Tick(playerProvider.CurrentObject, Time.deltaTime);
		}

		private void DestroyRemains()
		{
			remainsWorld?.Dispose();
			remainsWorld = null;
		}
	}
}
