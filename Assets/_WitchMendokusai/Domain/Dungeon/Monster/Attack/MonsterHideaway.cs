using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 공격 도중 사라진 몸 (HideMonsterAttack). 안 맞고 안 부딪히고, 그림은 떠올라 사라졌다가 떨어짐. 행동 트리 틱 (0.1초) 사이를 매 프레임 이음
	// 몸 하나에 하나. 끝나거나 끊기면 Return 이 모두 되돌림
	public sealed class MonsterHideaway : MonoBehaviour
	{
		private readonly List<Collider> disabledColliders = new();
		private MonsterObject monster;
		private MonsterPoseDriver pose;
		private PlayerProvider playerProvider;
		private HideMonsterAttack attack;
		private float leaveTime;
		private float lockTime;
		private float landTime;
		private Vector3 landing;
		private bool isAway;
		// 다시 나타났지만 주인공과 겹쳐 있어 충돌을 미루는 중
		private bool isSettling;
		private float settleEndTime;
		private float nextAimTime;
		private Vector3 aim;

		private bool HasLanded => Time.time >= landTime;

		public static MonsterHideaway Of(UnitObject unitObject)
		{
			if (unitObject.TryGetComponent(out MonsterHideaway hideaway) == false)
				hideaway = unitObject.gameObject.AddComponent<MonsterHideaway>();
			hideaway.monster = (MonsterObject)unitObject;
			hideaway.pose = MonsterPoseDriver.Of(unitObject);
			return hideaway;
		}

		public void Leave(HideMonsterAttack newAttack, PlayerProvider newPlayerProvider)
		{
			Return();
			Restore();
			attack = newAttack;
			playerProvider = newPlayerProvider;
			isAway = true;
			leaveTime = Time.time;
			lockTime = leaveTime + attack.ChaseSeconds;
			landTime = float.PositiveInfinity;
			nextAimTime = 0f;
			monster.SetHidden(true);
			monster.UnitMovement.enabled = false;
			foreach (Collider part in GetComponentsInChildren<Collider>())
			{
				if (part.enabled == false)
					continue;
				part.enabled = false;
				disabledColliders.Add(part);
			}
			Draw();
		}

		// 자리를 고정. strikeTime 에 position 에 닿음
		public void Land(Vector3 position, float strikeTime)
		{
			landing = position;
			landTime = strikeTime - attack.LandLeadSeconds;
		}

		// 다시 보이고 맞음. settles 면 부딪힘은 주인공과 떨어진 뒤 (때린 밀림이 먼저 떼어 놓게). 안 사라졌으면 무해
		public void Return(bool settles = false)
		{
			if (isAway == false)
				return;
			isAway = false;
			if (HasLanded)
				monster.UnitMovement.Reposition(landing);
			monster.SpriteRenderer.enabled = true;
			monster.SetHidden(false);
			isSettling = true;
			settleEndTime = Time.time + attack.SettleSeconds;
			if (settles == false || OverlapsPlayer() == false)
				Restore();
		}

		private bool OverlapsPlayer()
		{
			PlayerObject player = playerProvider.CurrentObject;
			return player != null && SlimeKingPatternMath.IsWithin(transform.position, player.transform.position, monster.UnitMovement.BodyRadius + player.UnitMovement.BodyRadius);
		}

		private void Restore()
		{
			if (isSettling == false)
				return;
			isSettling = false;
			foreach (Collider part in disabledColliders)
			{
				if (part != null)
					part.enabled = true;
			}
			disabledColliders.Clear();
			monster.UnitMovement.enabled = true;
			monster.UnitMovement.CancelImpulse();
		}

		private void OnDisable()
		{
			Return();
			Restore();
		}

		// 몸동작 (MonsterPoseDriver.LateUpdate) 보다 먼저
		private void Update()
		{
			if (isSettling && (Time.time >= settleEndTime || OverlapsPlayer() == false))
				Restore();
			if (isAway == false)
				return;
			if (float.IsPositiveInfinity(landTime) == false)
			{
				// 떠오른 것은 떨어질 자리 위로, 숨은 것은 나타날 때까지 제자리 (자동 조준이 진짜 자리를 알려 주지 않게)
				if (attack.RiseHeight > 0f || HasLanded)
					monster.UnitMovement.Reposition(landing);
			}
			else if (Time.time >= leaveTime + attack.RiseSeconds && Time.time < lockTime)
				Chase();
			Draw();
		}

		private void Chase()
		{
			// 장애물을 피한 자리 계산은 가끔만
			if (Time.time >= nextAimTime)
			{
				nextAimTime = Time.time + attack.AimInterval;
				aim = PlayerGround();
			}
			monster.UnitMovement.Reposition(Vector3.MoveTowards(transform.position, aim, attack.ChaseSpeed * Time.deltaTime));
		}

		// 뛰어오른 높이는 안 따라감. 벽 밖과 장애물 속은 그 앞까지
		private Vector3 PlayerGround()
		{
			PlayerObject player = playerProvider.CurrentObject;
			if (player == null)
				return transform.position;
			Vector3 position = player.transform.position;
			if (player.UnitMovement.IsGrounded() == false)
				position.y = transform.position.y;
			return Clamp(monster, position);
		}

		public static Vector3 Clamp(UnitObject unitObject, Vector3 position) =>
			DungeonManagerBridge.IsDungeon ? DungeonManager.Instance.MonsterSpawner.ClampToArena(position, unitObject.UnitMovement.BodyRadius) : position;

		private void Draw()
		{
			float risen = (Time.time - leaveTime) / attack.RiseSeconds;
			float left = landTime - Time.time;
			bool isFalling = left < attack.FallSeconds;
			bool isSeen = attack.RiseHeight > 0f && (isFalling || risen < 1f);
			monster.SpriteRenderer.enabled = isSeen;
			if (isSeen == false)
				return;
			float fallen = 1f - Mathf.Clamp01(left / attack.FallSeconds);
			float height = isFalling ? 1f - fallen * fallen : risen * risen;
			pose.Hold(attack.Pose, isFalling ? attack.FallSprite : attack.RiseSprite, attack.RiseHeight * height);
		}
	}
}
