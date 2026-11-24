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
			DisposeLostRemains();
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

		// 장면이 내려갈 때 (Play 종료, 로비로) 는 흔적 뿌리가 바닥보다 먼저 사라져 있을 수 있음.
		// 조각을 하나씩 비울 수 없으므로 묶음째 버려 전역값 (Current, 풀 셰이더 값) 을 되돌림
		private void DisposeLostRemains()
		{
			if (remainsWorld != null && remainsWorld.IsRootLost)
				DestroyRemains();
		}

		private void ReleaseRemains()
		{
			DisposeLostRemains();
			remainsWorld?.ReleaseAll();
		}

		private void DestroyRemains()
		{
			remainsWorld?.Dispose();
			remainsWorld = null;
		}
	}
}
