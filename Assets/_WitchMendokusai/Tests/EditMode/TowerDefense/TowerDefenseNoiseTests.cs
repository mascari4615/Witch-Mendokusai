using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 소리 규칙 회귀 — 내 행동이 마수를 부른다. 「멀찍이서 조용히」와 「옆에서 난사」가 달라야
	/// 개척의 위험이 거리 하나로 납작해지지 않는다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseNoiseTests
	{
		[Test]
		public void 아무_소리도_안_났으면_0()
		{
			TowerDefenseNoise noise = new();

			Assert.AreEqual(0f, noise.LevelAt(Vector3.zero, hearingRadius: 20f), 0.001f);
			Assert.AreEqual(0, noise.SourceCount);
		}

		[Test]
		public void 같은_자리에서_거듭_나면_자리가_아니라_크기가_는다()
		{
			// 같은 자리에서 스무 번 쏘면 자리가 스무 개 생기는 게 아니라 한 자리가 시끄러워야 한다.
			TowerDefenseNoise noise = new();

			for (int shot = 0; shot < 5; shot++)
				noise.Emit(new Vector3(10f, 0f, 0f), amount: 2f, mergeDistance: 3f);

			Assert.AreEqual(1, noise.SourceCount);
			Assert.AreEqual(10f, noise.LoudestLevel, 0.001f);
		}

		[Test]
		public void 멀리_떨어진_소리는_따로_센다()
		{
			TowerDefenseNoise noise = new();

			noise.Emit(new Vector3(10f, 0f, 0f), amount: 2f, mergeDistance: 3f);
			noise.Emit(new Vector3(60f, 0f, 0f), amount: 2f, mergeDistance: 3f);

			Assert.AreEqual(2, noise.SourceCount);
		}

		[Test]
		public void 들리는_거리_밖은_안_들린다()
		{
			TowerDefenseNoise noise = new();
			noise.Emit(new Vector3(50f, 0f, 0f), amount: 30f, mergeDistance: 3f);

			Assert.AreEqual(0f, noise.LevelAt(Vector3.zero, hearingRadius: 20f), 0.001f,
				"들리는 거리 밖까지 깨우면 판 전체가 한 번에 일어난다.");
		}

		[Test]
		public void 가까울수록_크게_들린다()
		{
			TowerDefenseNoise noise = new();
			noise.Emit(Vector3.zero, amount: 10f, mergeDistance: 3f);

			float near = noise.LevelAt(new Vector3(5f, 0f, 0f), hearingRadius: 20f);
			float far = noise.LevelAt(new Vector3(15f, 0f, 0f), hearingRadius: 20f);

			Assert.Greater(near, far);
		}

		[Test]
		public void 여러_소리는_더해진다()
		{
			// 사방에서 조금씩 나는 것도 모이면 깨울 수 있어야 「소리 사태」가 성립한다.
			TowerDefenseNoise noise = new();
			noise.Emit(new Vector3(5f, 0f, 0f), amount: 4f, mergeDistance: 1f);
			noise.Emit(new Vector3(-5f, 0f, 0f), amount: 4f, mergeDistance: 1f);

			float both = noise.LevelAt(Vector3.zero, hearingRadius: 20f);

			TowerDefenseNoise single = new();
			single.Emit(new Vector3(5f, 0f, 0f), amount: 4f, mergeDistance: 1f);

			Assert.Greater(both, single.LevelAt(Vector3.zero, hearingRadius: 20f) + 0.001f);
		}

		[Test]
		public void 소리는_비율로_잦아들고_결국_사라진다()
		{
			// 일정량씩 빼면 큰 소리는 영원히 남고 작은 소리는 한 틱에 사라진다 — 「잦아든다」가 안 생긴다.
			TowerDefenseNoise noise = new();
			noise.Emit(Vector3.zero, amount: 8f, mergeDistance: 1f);

			noise.Tick(deltaTime: 1f, decayPerSecond: 0.5f);
			Assert.AreEqual(4f, noise.LoudestLevel, 0.01f, "1초에 절반이면 8은 4가 돼야 한다.");

			noise.Tick(deltaTime: 30f, decayPerSecond: 0.5f);
			Assert.AreEqual(0, noise.SourceCount, "다 잦아든 자리가 남으면 목록이 무한히 자란다.");
		}

		[Test]
		public void 상한을_안_넘는다()
		{
			TowerDefenseNoise noise = new();
			for (int shot = 0; shot < 500; shot++)
				noise.Emit(Vector3.zero, amount: 10f, mergeDistance: 1f);

			Assert.AreEqual(TowerDefenseNoise.MAX_LEVEL, noise.LoudestLevel, 0.001f);
		}
	}
}
