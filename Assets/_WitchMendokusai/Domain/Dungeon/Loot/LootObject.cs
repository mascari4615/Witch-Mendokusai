using System.Collections;
using UnityEngine;
using VContainer;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public abstract class LootObject : MonoBehaviour
	{
		[SerializeField] private float moveSpeed = 1f;
		[SerializeField, Min(0f)] private float pickupDistance = 0.3f;
#pragma warning disable CS0414 // 베지어 커브 이동 미구현 예약 필드
		[SerializeField] private bool useBezierCurve = false;
#pragma warning restore CS0414

		private Coroutine _moveLoop;
		private Collider pickupCollider;
		private bool pickupColliderEnabled;

		// VContainer 는 `[Inject]` **메서드**를 base+자식 통틀어 1개만 코드생성한다(2개 이상이면 조용히
		// 리플렉션 폴백 + "generics" 라고 원인을 잘못 말하는 VCON0010). 자식(ItemObject)이 자기 Construct 를
		// 가져야 하므로 base 는 *필드 주입*을 쓴다 — 필드는 개수 제한이 없다.
		// (private 이면 같은 dll 에서 set 이 안 돼 또 폴백 = VCON0007. internal 이 최소 가시성.)
		// 정본: Domain/Application/DI/VCONTAINER-MECHANISM.md §3·§6
		[Inject] internal PlayerProvider playerProvider;

		private void Awake()
		{
			pickupCollider = GetComponent<Collider>();
			pickupColliderEnabled = pickupCollider != null && pickupCollider.enabled;
		}

		private void OnEnable()
		{
			StopAllCoroutines();
			_moveLoop = null;
			if (pickupCollider != null)
				pickupCollider.enabled = pickupColliderEnabled;
			ObjectBufferManager.AddObject(ObjectType.Drop, gameObject);
		}

		public void OnTriggerEnter(Collider other)
		{
			if (_moveLoop != null)
				return;

			if (other.CompareTag("PlayerExpCollider"))
			{
				Equip();
			}
		}

		public void Equip()
		{
			if (_moveLoop != null)
				return;

			// 흡수 중인 전리품의 중복 접촉 검사 생략
			if (pickupCollider != null)
				pickupCollider.enabled = false;
			_moveLoop = StartCoroutine(MoveLoop());
		}

		private IEnumerator MoveLoop()
		{
			while (true)
			{
				Vector3 target = playerProvider.Current.transform.position;
				Vector3 nextPosition = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
				transform.position = nextPosition;

				if ((nextPosition - target).sqrMagnitude <= pickupDistance * pickupDistance)
				{
					Effect();
					_moveLoop = null;
					break;
				}

				yield return null;
			}
		}

		public void Effect()
		{
			OnEffect();
			gameObject.SetActive(false);
		}
		protected abstract void OnEffect();

		private void OnDisable()
		{
			ObjectBufferManager.RemoveObject(ObjectType.Drop, gameObject);
		}
	}
}
