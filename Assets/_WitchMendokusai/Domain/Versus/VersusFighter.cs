using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 한 명 (TASK-WM-411). 즉사제 — 체력이 없고 「살았나 죽었나」뿐이다.
	/// 스탯은 자기가 안 갖고 <see cref="VersusMatchCore"/> 에서 매 라운드 받아온다(빌드의 정본은 판정 쪽).
	/// </summary>
	public sealed class VersusFighter : MonoBehaviour
	{
		private VersusArena arena;
		private IVersusInput input;
		private VersusTuning tuning;
		private VersusFighterStats stats;
		private Vector3 facing = Vector3.right;
		private float fireCooldown;
		private float dashCooldown;
		private int dashChargesLeft;
		private int shieldChargesLeft;

		public int PlayerIndex { get; private set; }
		public bool IsAlive { get; private set; }
		public float Radius => tuning.FighterRadius * stats.BodyScale;
		public int ShieldChargesLeft => shieldChargesLeft;
		public int DashChargesLeft => dashChargesLeft;

		public void Initialize(int playerIndex, VersusArena arena, IVersusInput input, VersusTuning tuning)
		{
			PlayerIndex = playerIndex;
			this.arena = arena;
			this.input = input;
			this.tuning = tuning;
		}

		/// <summary> 라운드 시작 — 카드로 두꺼워진 스탯을 받고 살아난다. </summary>
		public void BeginRound(VersusFighterStats roundStats)
		{
			stats = roundStats;
			IsAlive = true;
			fireCooldown = 0f;
			dashCooldown = 0f;
			dashChargesLeft = stats.DashCharges;
			shieldChargesLeft = stats.ShieldCharges;
			facing = PlayerIndex == 0 ? Vector3.right : Vector3.left;
			transform.position = arena.SpawnPosition(PlayerIndex);
			transform.localScale = Vector3.one * (tuning.FighterRadius * 2f * stats.BodyScale);
			gameObject.SetActive(true);
		}

		/// <summary> 한 프레임 굴린다. 탄을 쏴야 하면 방향을 돌려준다(발사 자체는 감독이 한다 — 탄 소유·풀은 감독 책임). </summary>
		public bool Tick(float deltaTime, out Vector3 fireDirection)
		{
			fireDirection = facing;

			if (IsAlive == false)
				return false;

			Vector2 move = input.ReadMove();
			Vector3 direction = new Vector3(move.x, 0f, move.y);

			if (direction.sqrMagnitude > 0.01f)
			{
				direction.Normalize();
				facing = direction;
				transform.position = arena.Clamp(transform.position + direction * (stats.MoveSpeed * deltaTime), Radius);
			}

			dashCooldown -= deltaTime;
			if (input.WasDashPressedThisFrame && dashChargesLeft > 0 && dashCooldown <= 0f)
			{
				dashChargesLeft--;
				dashCooldown = tuning.DashCooldownSeconds;
				transform.position = arena.Clamp(transform.position + facing * tuning.DashDistance, Radius);
			}

			fireCooldown -= deltaTime;
			if (input.IsFireHeld && fireCooldown <= 0f)
			{
				fireCooldown = stats.FireInterval;
				fireDirection = facing;
				return true;
			}

			return false;
		}

		/// <summary> 맞았다. 방패가 남아 있으면 한 번 무효(그리고 소모) — 죽었으면 true. </summary>
		public bool TakeHit()
		{
			if (IsAlive == false)
				return false;

			if (shieldChargesLeft > 0)
			{
				shieldChargesLeft--;
				return false;
			}

			IsAlive = false;
			gameObject.SetActive(false);
			return true;
		}

		/// <summary> 지금 스탯 — 감독이 탄을 만들 때 쓴다. </summary>
		public VersusFighterStats Stats => stats;
	}
}
