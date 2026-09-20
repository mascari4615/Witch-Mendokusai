using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	/// <summary>전투 사진을 아군과 적 표시 객체로 투영</summary>
	internal sealed class BattleEntityPresenter
	{
		/// <summary>무대가 넘기는 표시 설정</summary>
		internal sealed class Settings
		{
			public GameObject DollPrefab { get; set; }
			public GameObject FoePrefab { get; set; }
			public GameObject BossPrefab { get; set; }
			public float LungeSeconds { get; set; }
			public float HurtSeconds { get; set; }
			public float PositionCatchUp { get; set; }
			public float AllyHeadHeight { get; set; }
			public float DollFallbackRadius { get; set; }
			/// <summary>프리팹 모델 배율. Yawn2 원본 키 2.23 을 도형 인형(머리 1.3)에 맞춤</summary>
			public float DollModelScale { get; set; }
			public Vector3 DollBodyPosition { get; set; }
			public Vector3 DollBodyScale { get; set; }
			public Vector3 DollHeadPosition { get; set; }
			public Vector3 DollHeadScale { get; set; }
			public float AllyBarHeight { get; set; }
			public float AllyBarWidth { get; set; }
			public float AllyBarThickness { get; set; }
			public Vector3 DownedEuler { get; set; }
			public Vector3 DownedScale { get; set; }
			public float AllyWalkBobHeight { get; set; }
			public float AllyWalkBobFrequency { get; set; }
			public float AllyWalkBobPhaseStep { get; set; }
			public float AllyLungeDistance { get; set; }
			public float AllyHurtDistance { get; set; }
			public float FoeEntranceDistance { get; set; }
			public float FoeEntranceSpeed { get; set; }
			public float FoeEntranceThreshold { get; set; }
			/// <summary>새 적은 화면 오른쪽 가장자리에서 이만큼 더 밖에서 (사용자 2026-09-20: 생성이 보이면 안 됨)</summary>
			public float FoeSpawnMargin { get; set; }
			/// <summary>가장자리가 멀어도 이 초 안에는 제자리. 시뮬은 이미 싸우고 있음</summary>
			public float FoeEntranceSeconds { get; set; }
			/// <summary>등장 순간 커지는 시간. 0 이면 바로 제 크기</summary>
			public float FoeSpawnPopSeconds { get; set; }
			/// <summary>죽은 적이 쓰러져 사라지기까지. 한 방 보스도 이만큼은 보임</summary>
			public float FoeFallSeconds { get; set; }
			public float FoeSpinDegrees { get; set; }
			public float FoeBobHeight { get; set; }
			public float FoeBobFrequency { get; set; }
			public float FoeBobPhaseStep { get; set; }
			public float BossScale { get; set; }
			public float FoeHeight { get; set; }
			public float FoeRadius { get; set; }
			public float FoeHeadHeight { get; set; }
			public float FoePickRadius { get; set; }
			public float AimTargetScale { get; set; }
			public Color AimTargetColor { get; set; }
			public float AimTargetTint { get; set; }
			public float FoeMinHealthScale { get; set; }
			public float FoeBarHeight { get; set; }
			public float FoeBarWidth { get; set; }
			public float FoeBarThickness { get; set; }
			public int ShapeStagesPerStep { get; set; }
			public int BossShardCount { get; set; }
			public float BossShellRadius { get; set; }
			public float BossShellSpread { get; set; }
			public float BossShellSpinDegrees { get; set; }
			public float BossSpikeInset { get; set; }
			public float BossShardRadiusScale { get; set; }
			public float BossShardThickness { get; set; }
			public float BossShardLift { get; set; }
			public Vector3 BossShardEulerStep { get; set; }
			public float BossShellCatchUpShare { get; set; }
			public float BossShardSpinShare { get; set; }
			public int BossSpikeFromStage { get; set; }
			public int ColorDepthStage { get; set; }
			public float DepthSaturation { get; set; }
			public float DepthDarken { get; set; }
			public float BossGlow { get; set; }
			public float FoeFlashSeconds { get; set; }
			public float FoeFlashWhiten { get; set; }
			public float MeleeHeightShare { get; set; }
			public float RangedHeightShare { get; set; }
			public Color MyColor { get; set; }
			public Color EnemyColor { get; set; }
			public Color RangedEnemyColor { get; set; }
			public Color BossColor { get; set; }
			public Color BarBackColor { get; set; }
			public Color AllyBarColor { get; set; }
			public Color ReviveBarColor { get; set; }
			public Color EnemyBarColor { get; set; }
			public Color[] GradeColors { get; set; }
		}

		private readonly BattleAllyPresenter allies;
		private readonly BattleFoePresenter foes;

		public BattleEntityPresenter(Transform worldRoot, Settings settings)
		{
			allies = new BattleAllyPresenter(worldRoot, settings);
			foes = new BattleFoePresenter(worldRoot, settings);
		}

		/// <summary>판이 원점을 되감은 프레임. 인형과 적을 같은 만큼 단숨에 옮김 (Lerp 로 미끄러지면 뒤로 튐)</summary>
		public void Shift(float dx)
		{
			allies.Shift(dx);
			foes.Shift(dx);
		}

		/// <summary>다음 그리기에서 자리를 단숨에. 전환 막 뒤의 재배치용</summary>
		public void SnapNext()
		{
			allies.SnapNext();
			foes.SnapNext();
		}

		/// <summary>던전 그림. null 이면 본판 (구역 도형)</summary>
		public void SetFoeLook(DungeonSO dungeon) => foes.SetLook(dungeon);

		/// <summary>화면 오른쪽 가장자리의 세상 x. 새 적은 이 밖에서 들어온다. NaN 이면 모름</summary>
		public void SetSpawnEdge(float worldX) => foes.SetSpawnEdge(worldX);

		public void Render(IdleSnapshot snapshot, float delta)
		{
			allies.Render(snapshot, delta);
			foes.Render(snapshot, delta);
		}

		public void SetTimeScale(float scale) => allies.SetTimeScale(scale);

		public void PlayAllyAttack(int seat) => allies.PlayAttack(seat);

		public void PlayFoeHit(long index) => foes.PlayHit(index);

		public void PlayAllyHit(int seat) => allies.PlayHit(seat);

		public bool TryGetAllyHead(int seat, out Vector3 position) =>
			allies.TryGetHead(seat, out position);

		public bool TryGetFoeHead(long index, out Vector3 position) =>
			foes.TryGetHead(index, out position);

		public bool TryGetFoeImpact(long index, out Vector3 position, out Color color) =>
			foes.TryGetImpact(index, out position, out color);

		public bool TryPickFoe(UnityEngine.UIElements.IPanel panel, Vector2 panelPosition, out long foeIndex) =>
			foes.TryPick(panel, panelPosition, out foeIndex);

		public void SetAimTarget(long foeIndex) => foes.SetAimTarget(foeIndex);
	}
}
