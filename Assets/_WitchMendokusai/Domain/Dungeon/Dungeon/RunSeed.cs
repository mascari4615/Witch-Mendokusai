using UnityEngine;

namespace WitchMendokusai
{
	// 판마다 하나 정하는 시드. 판 단위로 다시 만들 수 있는 무작위 (바닥 장식, 부서지는 물건 배치) 가 여기서 갈라 씀
	// 카드와 전투 무작위는 쓰지 않음 (UnityEngine.Random 그대로)
	public static class RunSeed
	{
		public static int Current { get; private set; }

		// 판이 바뀔 때마다 증가. 소비자가 이전 판 결과를 버릴지 판단
		public static int Version { get; private set; }

		// 개발자 창 고정 시드. 값이 있으면 설정 자산보다 우선, 다음 판부터 적용
		public static int? DevOverride { get; private set; }

		public static void SetDevOverride(int? seed) => DevOverride = seed;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState()
		{
			Current = 0;
			Version = 0;
			DevOverride = null;
		}

		// 판 시작 (재도전 포함). 설정에 고정값이 켜져 있으면 그 값
		public static void Begin()
		{
			RunSeedSettings settings = Resources.Load<RunSeedSettings>(nameof(RunSeedSettings));
			if (settings == null)
				throw new System.InvalidOperationException($"Resources/{nameof(RunSeedSettings)} 없음");
			bool isFixed = DevOverride.HasValue || settings.UseFixedSeed;
			Current = DevOverride ?? (settings.UseFixedSeed ? settings.FixedSeed : new System.Random().Next());
			Version++;
			Debug.Log($"[{nameof(RunSeed)}] {Current}{(isFixed ? " (고정)" : string.Empty)}");
		}

		// 용도별 갈래. salt 는 쓰는 쪽의 직렬화 값 (장식 시드, 물건 시드)
		public static int Derive(int salt) => Derive(Current, salt);

		// 정해진 판 시드의 갈래. 개발자 창 예산 시뮬레이션처럼 현재 판이 아닌 시드
		public static int Derive(int runSeed, int salt)
		{
			unchecked
			{
				uint hash = (uint)runSeed * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
				hash ^= hash >> 16;
				hash *= 0x7FEB352Du;
				hash ^= hash >> 15;
				return (int)hash;
			}
		}
	}
}
