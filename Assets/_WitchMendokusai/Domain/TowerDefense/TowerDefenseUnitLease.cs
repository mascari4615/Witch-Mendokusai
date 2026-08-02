using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 풀에서 빌려온 유닛의 **원상복구 계약** — 개척이 유닛에 가한 변경을 반납 전에 전부 되돌린다.
	///
	/// ★ 왜 필요한가 (실측 근거): <see cref="ObjectPoolManager"/> 는 반납 시 아무것도 리셋하지 않는다
	///   (Push = SetActive(false) 뿐). 그런데 개척은 스폰한 유닛에 ① 애니메이터 정지 ② 역할 색 ③ 크기
	///   ④ UnitBrain 정지 ⑤ 자동시전 차단 ⑥ TacticDriver 부착 을 가한다. 반납해도 그대로 남으므로
	///   **다시 시작하면 지난 매치의 흔적을 뒤집어쓴 유닛이 나온다** — 게다가 코어/포탑/채집/마수가
	///   같은 프리팹(=같은 풀)을 쓰기 때문에 "지난번 마수였던 개체가 이번엔 코어"가 실제로 일어난다.
	///   역할 전용 부착물(TacticDriver)이 남으면 그때부터는 무엇이 그 유닛을 움직이는지 추적 불가.
	///
	/// ★ 계약: 빌릴 때 <see cref="Acquire"/>(최초 1회만 원본 상태 스냅샷) → 반납 전 <see cref="Release"/>(복구).
	///   "기본값이 이럴 것이다"를 추측하지 않고 **프리팹이 실제로 갖고 있던 값**을 되돌린다.
	/// </summary>
	public class TowerDefenseUnitLease : MonoBehaviour
	{
		private bool captured;

		private Animator[] animators;
		private bool[] animatorEnabled;
		private UnitBrain[] brains;
		private bool[] brainEnabled;

		// 영웅은 물리가 아니라 사람이 자리를 정한다 — 그래서 대여 중 강체를 kinematic 으로 바꾼다.
		// 같은 프리팹이 다음엔 마수로 나오므로 반납 때 원래대로 돌려놓지 않으면 「안 떨어지는 마수」가 된다.
		private Rigidbody body;
		private bool bodyKinematic;
		private bool bodyGravity;

		// 본편 이동 시스템(NavMesh 기반) — 개척은 자체 흐름장으로 걷고 지면도 런타임 생성이라 NavMesh 가 없다.
		// 켜둔 채로 두면 에이전트가 매 프레임 좌표를 자기 값으로 되돌려 「명령해도 안 움직이는」 영웅이 된다(실측).
		private UnityEngine.AI.NavMeshAgent navAgent;
		private bool navAgentEnabled;
		private UnitMovement movement;
		private bool movementEnabled;

		// 충돌 몸통 — 개척은 「보이는 크기 ≠ 물리 크기」로 만들려고 굵기를 손대므로 원본을 되돌려야 한다.
		private CapsuleCollider capsule;
		private float capsuleRadius;
		private float capsuleHeight;

		private bool hasSpriteRenderer;
		private Color spriteColor;
		private Vector3 localScale;
		private Quaternion localRotation;

		/// <summary>
		/// 개척이 손대기 *전에* 호출 — 최초 1회만 원본 상태를 스냅샷한다.
		/// (두 번째 대여부터는 직전 Release 로 이미 원본 상태이므로 다시 찍으면 안 된다.)
		/// </summary>
		public void Acquire(UnitObject unitObject)
		{
			if (captured)
				return;

			animators = GetComponentsInChildren<Animator>(true);
			animatorEnabled = new bool[animators.Length];
			for (int index = 0; index < animators.Length; index++)
				animatorEnabled[index] = animators[index].enabled;

			brains = GetComponents<UnitBrain>();
			brainEnabled = new bool[brains.Length];
			for (int index = 0; index < brains.Length; index++)
				brainEnabled[index] = brains[index].enabled;

			hasSpriteRenderer = unitObject != null && unitObject.SpriteRenderer != null;
			if (hasSpriteRenderer)
				spriteColor = unitObject.SpriteRenderer.color;

			capsule = GetComponent<CapsuleCollider>();
			if (capsule != null)
			{
				capsuleRadius = capsule.radius;
				capsuleHeight = capsule.height;
			}

			navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
			if (navAgent != null)
				navAgentEnabled = navAgent.enabled;

			movement = GetComponent<UnitMovement>();
			if (movement != null)
				movementEnabled = movement.enabled;

			body = GetComponent<Rigidbody>();
			if (body != null)
			{
				bodyKinematic = body.isKinematic;
				bodyGravity = body.useGravity;
			}

			localScale = transform.localScale;
			localRotation = transform.localRotation;

			captured = true;
		}

		/// <summary>
		/// 풀 반납 직전 호출 — 스냅샷 복원 + 역할 전용 부착물 제거.
		/// 스냅샷이 없으면(=한 번도 대여된 적 없음) 아무것도 하지 않는다(추측 복원 금지).
		/// </summary>
		public void Release(UnitObject unitObject)
		{
			// 역할 전용 부착물 — 다음 대여의 역할이 다를 수 있으므로 반드시 떼어낸다.
			TacticDriver driver = GetComponent<TacticDriver>();
			if (driver != null)
			{
				driver.StopDriving();
				Destroy(driver);
			}

			if (captured == false)
				return;

			for (int index = 0; index < animators.Length; index++)
			{
				if (animators[index] != null)
					animators[index].enabled = animatorEnabled[index];
			}

			for (int index = 0; index < brains.Length; index++)
			{
				if (brains[index] != null)
					brains[index].enabled = brainEnabled[index];
			}

			if (hasSpriteRenderer && unitObject != null && unitObject.SpriteRenderer != null)
				unitObject.SpriteRenderer.color = spriteColor;

			// 자동시전은 Init 을 건너뛰고 보존되는 값이라(UnitObject.Init 주석) 명시 복구가 필요하다.
			if (unitObject != null && unitObject.SkillHandler != null)
				unitObject.SkillHandler.AutoCastEnabled = true;

			if (capsule != null)
			{
				capsule.radius = capsuleRadius;
				capsule.height = capsuleHeight;
			}

			if (navAgent != null)
				navAgent.enabled = navAgentEnabled;
			if (movement != null)
				movement.enabled = movementEnabled;

			if (body != null)
			{
				body.isKinematic = bodyKinematic;
				body.useGravity = bodyGravity;
			}

			transform.localScale = localScale;
			transform.localRotation = localRotation;
		}
	}
}
