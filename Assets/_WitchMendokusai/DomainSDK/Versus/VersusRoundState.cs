using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary> 한 사람이 한 틱에 보내는 것 — 이게 네트워크로 흐르는 전부다(위치가 아니라 <b>의도</b>). </summary>
	public struct VersusInputFrame
	{
		public Vector2 Move;      // -1~1, 판 평면(x = 가로, y = 세로).
		public Vector2 Aim;       // 조준 방향(정규화). 2컴 대전이라 각자 마우스로 겨눈다.
		public bool Fire;
		public bool Dash;
	}

	/// <summary> 그릴 것 하나(사람 or 탄). 뷰는 이 목록만 보고 그린다 — 규칙을 모른다. </summary>
	public struct VersusBodyView
	{
		public Vector2 Position;
		public float Radius;
		public int Owner;
		public bool Alive;
	}

	/// <summary>
	/// 대결 한 라운드의 <b>규칙 전부</b> (TASK-WM-411). 엔진·네트워크·입력장치를 모른다.
	///
	/// ★ 왜 여기 있나 (2026-08-16, 「2컴 온라인 대전」으로 방향 확정 후):
	///   같은 규칙이 유니티 MonoBehaviour 와 시뮬에 두 벌로 있었다. 2컴 대전은 <b>심판이 하나여야</b>
	///   성립하는데(서로 자기 화면에서 이겼다고 하면 판이 안 선다), 그 심판은 서버다.
	///   그리고 `WM.Server` 는 「게임 규칙을 다시 구현하지 않고 같은 DomainSDK 를 참조한다」가 원칙이다.
	///   그래서 규칙을 여기로 올렸다 — <b>서버·유니티·시뮬·봇이 같은 한 벌</b>을 쓴다.
	///
	/// 60Hz 고정 틱. 같은 입력 · 같은 씨앗이면 어디서 돌려도 같은 결과(결정론).
	/// </summary>
	public sealed class VersusRoundState
	{
		public const float TICK = 1f / 60f;
		public const int PLAYER_COUNT = MatchConstants.VERSUS_PLAYER_COUNT;

		private readonly VersusTuning tuning;
		private readonly float halfWidth;
		private readonly float halfDepth;
		private readonly Fighter[] fighters = new Fighter[PLAYER_COUNT];
		private readonly List<Shot> shots = new List<Shot>();

		private struct Fighter
		{
			public Vector2 Position;
			public Vector2 Facing;
			public VersusFighterStats Stats;
			public float FireCooldown;
			public float DashCooldown;
			public int DashLeft;
			public int ShieldLeft;
			public bool Alive;
		}

		private struct Shot
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float LifeLeft;
			public float Radius;
			public int Owner;
			public int BouncesLeft;
		}

		public VersusRoundState(VersusFighterStats first, VersusFighterStats second, VersusTuning tuning,
			float halfWidth, float halfDepth, Vector2 firstSpawn, Vector2 secondSpawn)
		{
			this.tuning = tuning;
			this.halfWidth = halfWidth;
			this.halfDepth = halfDepth;

			fighters[0] = NewFighter(first, firstSpawn, new Vector2(1f, 0f));
			fighters[1] = NewFighter(second, secondSpawn, new Vector2(-1f, 0f));
		}

		public float ElapsedSeconds { get; private set; }

		/// <summary> 아직 안 끝났으면 false. 끝났으면 true 이고 <see cref="Winner"/> 가 답. </summary>
		public bool IsOver { get; private set; }

		/// <summary> 이긴 쪽(0/1) 또는 <see cref="VersusMatchCore.NO_WINNER"/>(동시사·시간초과). </summary>
		public int Winner { get; private set; } = VersusMatchCore.NO_WINNER;

		public bool IsAlive(int playerIndex) => fighters[playerIndex].Alive;
		public Vector2 PositionOf(int playerIndex) => fighters[playerIndex].Position;
		public float RadiusOf(int playerIndex) => tuning.FighterRadius * fighters[playerIndex].Stats.BodyScale;
		public int ShieldLeftOf(int playerIndex) => fighters[playerIndex].ShieldLeft;
		public int DashLeftOf(int playerIndex) => fighters[playerIndex].DashLeft;
		public int ShotCount => shots.Count;

		/// <summary> 지금 화면에 있는 탄 — 뷰가 그리거나, 서버가 클라에 보낼 목록. </summary>
		public void CollectShots(List<VersusBodyView> into)
		{
			into.Clear();

			for (int index = 0; index < shots.Count; index++)
			{
				into.Add(new VersusBodyView
				{
					Position = shots[index].Position,
					Radius = shots[index].Radius,
					Owner = shots[index].Owner,
					Alive = true,
				});
			}
		}


		// ── 되감기 (롤백 넷코드의 뼈대) ─────────────────────────────────────────

		/// <summary>
		/// 지금 판을 통째로 찍는다. 그리는 값이 아니라 <b>다시 굴리는 값</b> 전부 —
		/// 이걸로 되돌리면 같은 입력에 같은 미래가 나온다.
		/// </summary>
		public VersusRoundSnapshot Capture(int tick)
		{
			VersusRoundSnapshot snapshot = new VersusRoundSnapshot
			{
				tick = tick,
				elapsed = ElapsedSeconds,
				isOver = IsOver,
				winner = Winner,
				fighters = new VersusFighterSnapshot[PLAYER_COUNT],
				shots = new VersusShotSnapshot[shots.Count],
			};

			for (int index = 0; index < PLAYER_COUNT; index++)
			{
				Fighter fighter = fighters[index];
				snapshot.fighters[index] = new VersusFighterSnapshot
				{
					x = fighter.Position.x,
					y = fighter.Position.y,
					facingX = fighter.Facing.x,
					facingY = fighter.Facing.y,
					fireCooldown = fighter.FireCooldown,
					dashCooldown = fighter.DashCooldown,
					dashLeft = fighter.DashLeft,
					shieldLeft = fighter.ShieldLeft,
					alive = fighter.Alive,
				};
			}

			for (int index = 0; index < shots.Count; index++)
			{
				Shot shot = shots[index];
				snapshot.shots[index] = new VersusShotSnapshot
				{
					x = shot.Position.x,
					y = shot.Position.y,
					velocityX = shot.Velocity.x,
					velocityY = shot.Velocity.y,
					lifeLeft = shot.LifeLeft,
					radius = shot.Radius,
					owner = shot.Owner,
					bouncesLeft = shot.BouncesLeft,
				};
			}

			return snapshot;
		}

		/// <summary>
		/// 찍어 둔 자리로 되돌린다. 스탯(카드로 정해진 것)은 라운드 내내 안 바뀌므로 그대로 둔다 —
		/// 되돌리는 것은 <b>움직이는 값</b>뿐이다.
		/// </summary>
		public void Restore(VersusRoundSnapshot snapshot)
		{
			if (snapshot == null)
				return;

			ElapsedSeconds = snapshot.elapsed;
			IsOver = snapshot.isOver;
			Winner = snapshot.winner;

			for (int index = 0; index < PLAYER_COUNT && index < snapshot.fighters.Length; index++)
			{
				VersusFighterSnapshot saved = snapshot.fighters[index];
				Fighter fighter = fighters[index];

				fighter.Position = new Vector2(saved.x, saved.y);
				fighter.Facing = new Vector2(saved.facingX, saved.facingY);
				fighter.FireCooldown = saved.fireCooldown;
				fighter.DashCooldown = saved.dashCooldown;
				fighter.DashLeft = saved.dashLeft;
				fighter.ShieldLeft = saved.shieldLeft;
				fighter.Alive = saved.alive;

				fighters[index] = fighter;
			}

			shots.Clear();

			for (int index = 0; index < snapshot.shots.Length; index++)
			{
				VersusShotSnapshot saved = snapshot.shots[index];
				shots.Add(new Shot
				{
					Position = new Vector2(saved.x, saved.y),
					Velocity = new Vector2(saved.velocityX, saved.velocityY),
					LifeLeft = saved.lifeLeft,
					Radius = saved.radius,
					Owner = saved.owner,
					BouncesLeft = saved.bouncesLeft,
				});
			}
		}

		/// <summary>
		/// 지금 판을 숫자 하나로 줄인다 — 두 기계가 <b>같은 판을 보고 있나</b>를 싸게 대조하는 용도.
		/// 어긋나면(desync) 그 자리에서 알 수 있어야 「왜 저쪽에서만 맞았지」를 추측으로 쫓지 않는다.
		/// </summary>
		public int Fingerprint()
		{
			unchecked
			{
				int hash = 17;

				for (int index = 0; index < PLAYER_COUNT; index++)
				{
					Fighter fighter = fighters[index];
					hash = hash * 31 + Quantize(fighter.Position.x);
					hash = hash * 31 + Quantize(fighter.Position.y);
					hash = hash * 31 + Quantize(fighter.FireCooldown);
					hash = hash * 31 + fighter.DashLeft;
					hash = hash * 31 + fighter.ShieldLeft;
					hash = hash * 31 + (fighter.Alive ? 1 : 0);
				}

				hash = hash * 31 + shots.Count;

				for (int index = 0; index < shots.Count; index++)
				{
					Shot shot = shots[index];
					hash = hash * 31 + Quantize(shot.Position.x);
					hash = hash * 31 + Quantize(shot.Position.y);
					hash = hash * 31 + shot.BouncesLeft;
				}

				return hash;
			}
		}

		// 소수점 끝자리는 기계마다 다를 수 있다 — 1000분의 1 로 잘라 비교한다(0.001 = 눈에 안 보이는 차이).
		private static int Quantize(float value)
		{
			return (int)(value * 1000f);
		}

		/// <summary> 이 자리 근처에 <b>남의 탄</b>이 있나 — 봇이 피할지 정할 때 쓴다(판이 답하고 봇은 묻기만 한다). </summary>
		public bool HasIncomingShot(Vector2 position, int selfIndex, float radius)
		{
			for (int index = 0; index < shots.Count; index++)
			{
				if (shots[index].Owner == selfIndex)
					continue;

				float dx = shots[index].Position.x - position.x;
				float dy = shots[index].Position.y - position.y;

				if (dx * dx + dy * dy <= radius * radius)
					return true;
			}

			return false;
		}

		/// <summary>
		/// 한 틱 진행. <paramref name="inputs"/> = 두 사람의 의도. 라운드가 <b>이번 틱에 끝났으면</b> true.
		/// </summary>
		public bool Step(VersusInputFrame[] inputs, float timeLimitSeconds)
		{
			if (IsOver)
				return false;

			ElapsedSeconds += TICK;

			for (int index = 0; index < PLAYER_COUNT; index++)
				StepFighter(index, inputs[index]);

			StepShots();
			ResolveHits();

			bool firstAlive = fighters[0].Alive;
			bool secondAlive = fighters[1].Alive;

			if (firstAlive && secondAlive)
			{
				if (timeLimitSeconds > 0f && ElapsedSeconds >= timeLimitSeconds)
				{
					IsOver = true;
					Winner = VersusMatchCore.NO_WINNER;
					return true;
				}

				return false;
			}

			IsOver = true;

			// 동시사 = 무승부. 즉사제에선 실제로 자주 나므로 「먼저 죽은 쪽」을 억지로 가르지 않는다.
			Winner = firstAlive == secondAlive ? VersusMatchCore.NO_WINNER : (firstAlive ? 0 : 1);
			return true;
		}

		// ── 내부 ──────────────────────────────────────────────────────────────

		private Fighter NewFighter(VersusFighterStats stats, Vector2 position, Vector2 facing)
		{
			return new Fighter
			{
				Position = position,
				Facing = facing,
				Stats = stats,
				DashLeft = stats.DashCharges,
				ShieldLeft = stats.ShieldCharges,
				Alive = true,
			};
		}

		private void StepFighter(int playerIndex, VersusInputFrame input)
		{
			Fighter self = fighters[playerIndex];

			if (self.Alive == false)
				return;

			float radius = tuning.FighterRadius * self.Stats.BodyScale;
			Vector2 move = Normalized(input.Move);

			if (move.x != 0f || move.y != 0f)
			{
				self.Facing = move;
				self.Position = Clamp(new Vector2(
					self.Position.x + move.x * self.Stats.MoveSpeed * TICK,
					self.Position.y + move.y * self.Stats.MoveSpeed * TICK), radius);
			}

			// 대시는 <b>조준한 쪽</b>이 아니라 <b>가려던 쪽</b>으로 — 회피 기술이지 돌진기가 아니다.
			self.DashCooldown -= TICK;
			if (input.Dash && self.DashLeft > 0 && self.DashCooldown <= 0f)
			{
				self.DashLeft--;
				self.DashCooldown = tuning.DashCooldownSeconds;
				self.Position = Clamp(new Vector2(
					self.Position.x + self.Facing.x * tuning.DashDistance,
					self.Position.y + self.Facing.y * tuning.DashDistance), radius);
			}

			self.FireCooldown -= TICK;
			if (input.Fire && self.FireCooldown <= 0f)
			{
				self.FireCooldown = self.Stats.FireInterval;
				Vector2 aim = Normalized(input.Aim);
				Fire(self, playerIndex, (aim.x != 0f || aim.y != 0f) ? aim : self.Facing, radius);
			}

			fighters[playerIndex] = self;
		}

		private void Fire(Fighter shooter, int ownerIndex, Vector2 aim, float bodyRadius)
		{
			int count = shooter.Stats.ProjectileCount;
			float spread = count > 1 ? tuning.ProjectileSpreadDegrees : 0f;
			float start = -spread * (count - 1) * 0.5f;

			// 곡사(중력)는 높이를 안 재고 <b>사거리 단축</b>으로 근사한다 — 평면 판정 한 벌로 서버·화면을 맞추기 위해서다.
			float lifeScale = shooter.Stats.ProjectileGravity > 0f
				? tuning.ProjectileLifeSeconds / (tuning.ProjectileLifeSeconds + shooter.Stats.ProjectileGravity)
				: 1f;

			for (int index = 0; index < count; index++)
			{
				Vector2 direction = Rotate(aim, (start + spread * index) * Mathf.Deg2Rad);

				shots.Add(new Shot
				{
					Position = new Vector2(
						shooter.Position.x + direction.x * (bodyRadius + 0.4f),
						shooter.Position.y + direction.y * (bodyRadius + 0.4f)),
					Velocity = new Vector2(direction.x * shooter.Stats.ProjectileSpeed, direction.y * shooter.Stats.ProjectileSpeed),
					Owner = ownerIndex,
					BouncesLeft = shooter.Stats.BounceCount,
					LifeLeft = tuning.ProjectileLifeSeconds * lifeScale,
					Radius = tuning.ProjectileRadius * shooter.Stats.ProjectileScale,
				});
			}
		}

		private void StepShots()
		{
			for (int index = shots.Count - 1; index >= 0; index--)
			{
				Shot shot = shots[index];
				shot.LifeLeft -= TICK;

				if (shot.LifeLeft <= 0f)
				{
					shots.RemoveAt(index);
					continue;
				}

				shot.Position = new Vector2(shot.Position.x + shot.Velocity.x * TICK, shot.Position.y + shot.Velocity.y * TICK);

				bool hitX = shot.Position.x < -halfWidth || shot.Position.x > halfWidth;
				bool hitY = shot.Position.y < -halfDepth || shot.Position.y > halfDepth;

				if (hitX || hitY)
				{
					if (shot.BouncesLeft <= 0)
					{
						shots.RemoveAt(index);
						continue;
					}

					shot.BouncesLeft--;

					if (hitX)
					{
						shot.Velocity = new Vector2(-shot.Velocity.x, shot.Velocity.y);
						shot.Position = new Vector2(Mathf.Clamp(shot.Position.x, -halfWidth, halfWidth), shot.Position.y);
					}

					if (hitY)
					{
						shot.Velocity = new Vector2(shot.Velocity.x, -shot.Velocity.y);
						shot.Position = new Vector2(shot.Position.x, Mathf.Clamp(shot.Position.y, -halfDepth, halfDepth));
					}
				}

				shots[index] = shot;
			}
		}

		private void ResolveHits()
		{
			for (int index = shots.Count - 1; index >= 0; index--)
			{
				Shot shot = shots[index];

				for (int playerIndex = 0; playerIndex < PLAYER_COUNT; playerIndex++)
				{
					Fighter target = fighters[playerIndex];

					if (target.Alive == false || shot.Owner == playerIndex)
						continue;

					float combined = tuning.FighterRadius * target.Stats.BodyScale + shot.Radius;
					float dx = target.Position.x - shot.Position.x;
					float dy = target.Position.y - shot.Position.y;

					if (dx * dx + dy * dy > combined * combined)
						continue;

					if (target.ShieldLeft > 0)
						target.ShieldLeft--;
					else
						target.Alive = false;

					fighters[playerIndex] = target;
					shots.RemoveAt(index);
					break;
				}
			}
		}

		private Vector2 Clamp(Vector2 position, float radius)
		{
			return new Vector2(
				Mathf.Clamp(position.x, -halfWidth + radius, halfWidth - radius),
				Mathf.Clamp(position.y, -halfDepth + radius, halfDepth - radius));
		}

		private static Vector2 Normalized(Vector2 value)
		{
			float length = Mathf.Sqrt(value.x * value.x + value.y * value.y);
			return length > 0.001f ? new Vector2(value.x / length, value.y / length) : new Vector2(0f, 0f);
		}

		private static Vector2 Rotate(Vector2 value, float radians)
		{
			float cos = Mathf.Cos(radians);
			float sin = Mathf.Sin(radians);
			return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
		}
	}
}
