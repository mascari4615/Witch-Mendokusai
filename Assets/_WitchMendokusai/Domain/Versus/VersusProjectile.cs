using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 탄 (TASK-WM-411). 물리 엔진에 맡기지 않고 직접 굴린다 —
	/// 반사 횟수·중력·크기가 전부 카드로 흔들리는 값이라, 판정을 한 자리에 두는 편이 「왜 저렇게 날았나」를 설명한다.
	/// </summary>
	public sealed class VersusProjectile : MonoBehaviour
	{
		private VersusArena arena;
		private VersusTuning tuning;
		private Vector3 velocity;
		private float gravity;
		private int bouncesLeft;
		private int ownerIndex;
		private float aliveSeconds;

		/// <summary> 쏜 사람. 자기 탄에는 잠깐 안 맞는다(총구가 몸 안에 있어 바로 자살하는 것 방지). </summary>
		public int OwnerIndex => ownerIndex;

		public void Launch(VersusArena arena, VersusTuning tuning, int ownerIndex, Vector3 direction, VersusFighterStats stats)
		{
			this.arena = arena;
			this.tuning = tuning;
			this.ownerIndex = ownerIndex;
			velocity = direction.normalized * stats.ProjectileSpeed;
			gravity = stats.ProjectileGravity;
			bouncesLeft = stats.BounceCount;
			aliveSeconds = 0f;
			transform.localScale = Vector3.one * (tuning.ProjectileRadius * 2f * stats.ProjectileScale);
		}

		private void Update()
		{
			float deltaTime = Time.deltaTime;
			aliveSeconds += deltaTime;

			if (aliveSeconds >= tuning.ProjectileLifeSeconds)
			{
				Destroy(gameObject);
				return;
			}

			if (gravity > 0f)
				velocity.y -= gravity * deltaTime;

			transform.position += velocity * deltaTime;
			ResolveWalls();
			ResolveFloor();
		}

		// 벽에 닿으면 반사 카드가 남은 만큼 튕기고, 다 쓰면 사라진다.
		private void ResolveWalls()
		{
			Vector3 position = transform.position;
			float halfWidth = arena.HalfWidth;
			float halfDepth = arena.HalfDepth;

			bool hitX = position.x < -halfWidth || position.x > halfWidth;
			bool hitZ = position.z < -halfDepth || position.z > halfDepth;

			if (hitX == false && hitZ == false)
				return;

			if (bouncesLeft <= 0)
			{
				Destroy(gameObject);
				return;
			}

			bouncesLeft--;

			if (hitX)
			{
				velocity.x = -velocity.x;
				position.x = Mathf.Clamp(position.x, -halfWidth, halfWidth);
			}

			if (hitZ)
			{
				velocity.z = -velocity.z;
				position.z = Mathf.Clamp(position.z, -halfDepth, halfDepth);
			}

			transform.position = position;
		}

		// 곡사(중력) 탄은 바닥에 닿으면 끝 — 이게 없으면 땅을 뚫고 영원히 떨어진다.
		private void ResolveFloor()
		{
			if (transform.position.y > 0.1f)
				return;

			Destroy(gameObject);
		}

		/// <summary> 이 탄이 <paramref name="target"/> 을 맞혔나. 즉사제라 「맞았다/아니다」 하나면 충분. </summary>
		public bool Overlaps(Vector3 target, float targetRadius)
		{
			float combined = targetRadius + transform.localScale.x * 0.5f;
			Vector3 difference = target - transform.position;
			difference.y = 0f;
			return difference.sqrMagnitude <= combined * combined;
		}

		/// <summary> 쏘고 난 직후엔 자기 탄에 안 맞는다. </summary>
		public bool CanHit(int targetIndex) => targetIndex != ownerIndex || aliveSeconds > 0.25f;
	}
}
