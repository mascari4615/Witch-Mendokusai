using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 판 동안 바닥에 남는 것 묶음: 잔해 (DebrisField), 자국 (GroundStainCanvas), 액체 (GoopField)
	/// - 생존 던전 바닥 (GroundGenerator) 이 만들고 매 프레임 돌림. 판 시작과 끝에 비움
	/// - 다른 기능 (상자, 몬스터 사망, 폭발) 은 Current 로 접근. 던전 밖이면 null 이고 남길 것이 없음
	/// - 유지 시간은 설정 창 값 (CombatEffectSettings.RemainsLingerSeconds). 끔이면 조각과 자국을 새로 남기지 않음
	/// - 액체는 놀이 (불, 감전, 느려짐) 라서 흔적 설정과 무관하게 돎
	/// </summary>
	public sealed class RemainsWorld
	{
		public static RemainsWorld Current { get; private set; }

		private readonly Transform root;

		public RemainsSettings Settings { get; }
		public DebrisField Debris { get; }
		public GroundStainCanvas Stains { get; }
		public GoopField Goop { get; }
		public GrassInteraction Grass { get; }
		public float GroundHeight { get; set; }

		private float killBudget;

		// 끔이면 상자 조각은 예전처럼 잠깐 날다 사라지고 자국과 액체는 안 남김
		public bool Enabled => LingerSeconds > 0f;
		public float LingerSeconds => CombatEffectSettings.RemainsLingerSeconds;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetCurrent() => Current = null;

		public RemainsWorld()
		{
			Settings = RemainsSettings.Load();
			GoopFire.RequirePrefab(Settings.FirePrefab);
			root = new GameObject("RemainsWorld").transform;
			Debris = new DebrisField(root);
			Stains = new GroundStainCanvas(root, Settings.StainUtilityShader, Settings.GroundStainShader);
			Goop = new GoopField(root, Settings);
			Grass = new GrassInteraction(root, Settings);
			Debris.OnStain += Stain;
			Goop.OnScorch += Stain;
			Current = this;
		}

		public bool IsRootLost => root == null;

		// 처치가 몰리면 조각은 일부만 (예산은 초당 KillPieceBudgetPerSecond 만큼 참)
		public bool TryConsumeKillBudget()
		{
			if (killBudget < 1f)
				return false;
			killBudget -= 1f;
			return true;
		}

		public void Tick(PlayerObject player, float deltaTime)
		{
			killBudget = Mathf.Min(Settings.KillPieceBudgetPerSecond, killBudget + Settings.KillPieceBudgetPerSecond * deltaTime);
			float linger = Enabled ? LingerSeconds : 0f;
			Vector3 focus = player.UnitMovement.Position;
			// 끔일 때도 이미 남은 것은 짧게 정리되도록 사라짐 시간만 씀
			Debris.Tick(Settings, linger, deltaTime, focus, player.UnitMovement.Velocity, true);
			Stains.SetVisible(Enabled);
			if (Enabled)
				Stains.Tick(Settings, linger, focus, GroundHeight, deltaTime);
			Goop.SetVisible(true);
			Goop.Tick(deltaTime, focus, GroundHeight, player);
			Grass.Tick(Settings, player, GroundHeight, deltaTime);
		}

		// 결과 화면처럼 판 밖: 날던 것만 마저 떨어뜨림, 밀지 않음
		public void TickIdle(float deltaTime)
		{
			float linger = Enabled ? LingerSeconds : 0f;
			Debris.Tick(Settings, linger, deltaTime, Vector3.zero, Vector3.zero, false);
			Grass.Suspend();
			// 판 밖에는 자국과 웅덩이를 안 보임 (멈춘 불이 남지 않게)
			Stains.SetVisible(false);
			Goop.SetVisible(false);
		}

		public void Stain(Vector3 position, float radius, Color color)
		{
			if (Enabled)
				Stains.Add(position, radius, color);
		}

		// 폭발과 돌진. 누운 조각을 날리고 불붙는 액체에 불을 붙임
		public void Blast(Vector3 center, float radius, float speed, float lift, bool ignite)
		{
			Debris.Push(center, radius, speed, lift);
			Grass.Flatten(center, radius * Settings.GrassBlastFlattenScale);
			if (ignite)
				Goop.Ignite(center, radius);
		}

		public void ReleaseAll()
		{
			Debris.Clear();
			Stains.Clear();
			Goop.Clear();
			Grass.Clear();
		}

		public void Dispose()
		{
			if (Current == this)
				Current = null;
			Stains.Dispose();
			Debris.Dispose();
			Goop.Dispose();
			Grass.Dispose();
			if (root != null)
				Object.Destroy(root.gameObject);
		}
	}
}
