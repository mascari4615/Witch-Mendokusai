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

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState()
		{
			Current = 0;
			Version = 0;
		}

		// 판 시작 (재도전 포함). 설정에 고정값이 켜져 있으면 그 값
		public static void Begin()
		{
			RunSeedSettings settings = Resources.Load<RunSeedSettings>(nameof(RunSeedSettings));
			if (settings == null)
				throw new System.InvalidOperationException($"Resources/{nameof(RunSeedSettings)} 없음");
			Current = settings.UseFixedSeed ? settings.FixedSeed : new System.Random().Next();
			Version++;
			Debug.Log($"[{nameof(RunSeed)}] {Current}{(settings.UseFixedSeed ? " (고정)" : string.Empty)}");
		}

		// 용도별 갈래. salt 는 쓰는 쪽의 직렬화 값 (장식 시드, 물건 시드)
		public static int Derive(int salt)
		{
			unchecked
			{
				uint hash = (uint)Current * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
				hash ^= hash >> 16;
				hash *= 0x7FEB352Du;
				hash ^= hash >> 15;
				return (int)hash;
			}
		}
	}
}
