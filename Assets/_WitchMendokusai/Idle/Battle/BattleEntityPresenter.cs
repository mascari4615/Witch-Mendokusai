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
			public float PopSeconds { get; set; }
			public float PositionCatchUp { get; set; }
			public float FoeEntranceDistance { get; set; }
			public float FoeEntranceSpeed { get; set; }
			public float FoeSpinDegrees { get; set; }
			public float FoeBobHeight { get; set; }
			public float BossScale { get; set; }
			public float FoeHeight { get; set; }
			public float FoeRadius { get; set; }
			public int ShapeStagesPerStep { get; set; }
			public int BossShardCount { get; set; }
			public float BossShellRadius { get; set; }
			public float BossShellSpread { get; set; }
			public float BossShellSpinDegrees { get; set; }
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

		public void Render(IdleSnapshot snapshot, float delta)
		{
			allies.Render(snapshot, delta);
			foes.Render(snapshot, delta);
		}

		public void PlayAllyAttack(int seat) => allies.PlayAttack(seat);

		public void PlayFoeHit(long index) => foes.PlayHit(index);

		public void PlayAllyHit(int seat) => allies.PlayHit(seat);

		public bool TryGetAllyHead(int seat, out Vector3 position) =>
			allies.TryGetHead(seat, out position);

		public bool TryGetFoeHead(long index, out Vector3 position) =>
			foes.TryGetHead(index, out position);

		public bool TryGetFoeImpact(long index, out Vector3 position, out Color color) =>
			foes.TryGetImpact(index, out position, out color);

		public bool TryPickFoe(Vector2 panelPosition, out long foeIndex) =>
			foes.TryPick(panelPosition, out foeIndex);
	}
}
