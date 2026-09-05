using UnityEngine;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// 인형 모델의 동작 스위치. 무대가 사진(IdleSnapshot)을 읽고 여기 스위치만 켜고 끔
	///
	/// ★ 판정은 전부 코어. 여기는 걷기, 공격, 피격, 쓰러짐 넷만
	/// ★ 에디트 모드 미리보기는 플레이어 루프가 안 돌아 Animator 가 멈춤. <see cref="Tick"/> 이 직접 밈
	/// </summary>
	public sealed class IdleDollAnimator : MonoBehaviour
	{
		private static readonly int MOVE = Animator.StringToHash("MOVE");
		private static readonly int DOWN = Animator.StringToHash("DOWN");
		private static readonly int ATTACK = Animator.StringToHash("ATTACK");
		private static readonly int HIT = Animator.StringToHash("HIT");

		[SerializeField] private Animator animator;

		public void SetMoving(bool moving)
		{
			animator.SetBool(MOVE, moving);
		}

		public void SetDowned(bool downed)
		{
			animator.SetBool(DOWN, downed);
		}

		public void PlayAttack()
		{
			animator.SetTrigger(ATTACK);
		}

		public void PlayHit()
		{
			animator.SetTrigger(HIT);
		}

		/// <summary>세상 시간 배율. 조준 중 느려짐</summary>
		public void SetSpeed(float scale)
		{
			animator.speed = scale;
		}

		public void Tick(float delta)
		{
			if (Application.isPlaying == false)
			{
				animator.Update(delta);
			}
		}
	}
}
