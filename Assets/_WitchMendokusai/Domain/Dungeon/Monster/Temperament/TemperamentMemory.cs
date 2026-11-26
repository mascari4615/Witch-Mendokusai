using UnityEngine;

namespace WitchMendokusai
{
	// 유닛 하나의 성격 기억. 성격마다 뜻을 정해 씀 (칸 이름은 넓게, 쓰는 법은 각 성격의 주석)
	// 풀에서 다시 꺼내질 때마다 Reset
	public sealed class TemperamentMemory
	{
		// 다음 사건 시각 (깡충 차례, 옆걸음 바꾸기 등)
		public float NextTime;
		// 성격 안의 단계 (쉼, 뜀, 깸 등)
		public int Phase;
		// 돌아갈 자리 (태어난 곳, 쉬는 구석)
		public Vector3 Anchor;
		// 화남, 깸 같은 켜짐 상태
		public bool IsOn;
		// 끝난 시각 (화가 풀릴 때 등)
		public float UntilTime;
		// 돌거나 비키는 쪽 +1, -1
		public float Side = 1f;
		// 유닛마다 다른 0~1 값. 같은 성격끼리 박자가 겹치지 않게
		public float Seed;
		// 이미 알아챈 사건의 시각 (복수가 같은 처치에 두 번 화나지 않게). 없으면 음의 무한대
		public float NoticedTime = float.NegativeInfinity;

		public void Reset(Vector3 anchor, float seed)
		{
			NextTime = 0f;
			Phase = 0;
			Anchor = TemperamentContext.Flat(anchor);
			IsOn = false;
			UntilTime = 0f;
			Seed = seed;
			NoticedTime = float.NegativeInfinity;
			Side = seed < 0.5f ? -1f : 1f;
		}
	}
}
