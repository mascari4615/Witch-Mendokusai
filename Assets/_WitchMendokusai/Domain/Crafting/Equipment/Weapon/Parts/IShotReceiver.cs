using UnityEngine;

namespace WitchMendokusai
{
	// Spawner 가 낸 한 발의 정보. 같은 번에 나간 발들 중 몇 번째인지와 조준 방향
	public readonly struct ShotInfo
	{
		public readonly int Index;
		public readonly int Count;
		public readonly Vector3 Direction;

		public ShotInfo(int index, int count, Vector3 direction)
		{
			Index = index;
			Count = count;
			Direction = direction;
		}
	}

	// 낸 발의 순번과 방향을 받아 움직임을 정하는 부품. 활성화 직전에 불림
	public interface IShotReceiver
	{
		void OnShot(ShotInfo shot);
	}
}
