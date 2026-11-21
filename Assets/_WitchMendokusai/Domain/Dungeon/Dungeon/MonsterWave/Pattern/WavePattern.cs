using UnityEngine;

namespace WitchMendokusai
{
	// 웨이브 패턴 하나. 자산은 설정만, 판마다 CreateRun 으로 실행 상태를 따로 만듦
	// 새 패턴: 이 클래스를 상속한 SO 하나와 WavePatternRun 상속 하나
	public abstract class WavePattern : ScriptableObject
	{
		public abstract WavePatternRun CreateRun();

		// 판 시작 전 설정 검사. 빠진 적 참조는 여기서 예외
		public abstract void Validate();

		protected void RequireMonsters(Monster[] monsters, string fieldName)
		{
			if (monsters == null || monsters.Length == 0)
				throw new System.InvalidOperationException($"{name}: {fieldName} 비어 있음");
			foreach (Monster monster in monsters)
				if (monster == null)
					throw new System.InvalidOperationException($"{name}: {fieldName} 에 빈 칸");
		}
	}

	// 한 판 안에서 돌아가는 패턴 하나의 상태
	public abstract class WavePatternRun
	{
		// localSeconds 는 패턴 시작부터 지난 초. 끝났으면 true
		public abstract bool Tick(IWavePatternHost host, float localSeconds, float deltaSeconds);

		// 기존 MonsterWave 주기 생성에 곱할 배율. 숨 고르기 패턴만 바꿈
		public virtual float PeriodicRateMultiplier(float localSeconds) => 1f;
	}

	// 패턴이 생성기에 요청하는 일
	public interface IWavePatternHost
	{
		Vector3 PlayerPosition { get; }
		Transform PlayerTransform { get; }

		// 주인공 이동 속도 (m/s). 방향은 입력, 크기는 실제 수평 속도 (SurvivalThreatRules.MoveVelocity). 예측 자리와 가로지름 방향
		Vector3 PlayerMoveVelocity { get; }

		// 숨 고르기 패턴과 최종 보스 뒤 배율을 합친 주기 생성률 배율
		float PeriodicRateMultiplier { get; }

		// 최종 보스 등장 뒤 배율만. 등장 전 1
		float FinalBossRateMultiplier { get; }

		// 위협 단계 물량 배율을 입힌 초당 생성 수
		float ScaleSpawnRate(float perSecond);

		// 위협 단계 물량 배율을 입힌 무리 크기
		int ScaleSwarmCount(int count);

		// 상한을 적용한 허용 마릿수
		int AllowedCount(SpawnClass spawnClass, int requested);

		// 기존 주기 웨이브와 같은 자리 고르기 (플레이어 진행 방향 앞, 아니면 둘레)
		void SpawnAroundPlayer(Monster monster);

		// 지정 위치 생성. moveOrder 가 있으면 생성 뒤 그 이동을 따름
		void SpawnAt(Monster monster, Vector3 position, WaveMoveOrder moveOrder);
	}
}
