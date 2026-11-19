using System.Collections;
using UnityEngine;
using VContainer;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public abstract class LootObject : MonoBehaviour
	{
		[SerializeField, Tooltip("자석으로 끌려올 때 초당 이동 거리")] private float moveSpeed = 1f;
		[SerializeField, Min(0f), Tooltip("자석으로 끌려올 때 이 거리 안이면 획득")] private float pickupDistance = 0.3f;

		private Coroutine _moveLoop;
		private Collider pickupCollider;
		private bool pickupColliderEnabled;

		// VContainer 는 `[Inject]` **메서드**를 base+자식 통틀어 1개만 코드생성한다(2개 이상이면 조용히
		// 리플렉션 폴백 + "generics" 라고 원인을 잘못 말하는 VCON0010). 자식(ItemObject)이 자기 Construct 를
		// 가져야 하므로 base 는 *필드 주입*을 쓴다 — 필드는 개수 제한이 없다.
		// (private 이면 같은 dll 에서 set 이 안 돼 또 폴백 = VCON0007. internal 이 최소 가시성.)
		// 정본: Domain/Application/DI/VCONTAINER-MECHANISM.md §3·§6
		[Inject] internal PlayerProvider playerProvider;
		[Inject] internal ResourceManager resourceManager;

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
				BeginPull(PullLoop());
			}
		}

		// 자석. 거리가 멀어 곡선 대신 일정 속도로 끌림
		public void Equip() => BeginPull(MoveLoop());

		private void BeginPull(IEnumerator loop)
		{
			if (_moveLoop != null)
				return;

			// 흡수 중인 전리품의 중복 접촉 검사 생략
			if (pickupCollider != null)
				pickupCollider.enabled = false;
			_moveLoop = StartCoroutine(loop);
		}

		// 줍기 범위 안. 처음 바깥으로 살짝 튀었다가 빨려 옴 (mc28)
		private IEnumerator PullLoop()
		{
			Vector3 startOffset = transform.position - playerProvider.Current.transform.position;
			float elapsed = 0f;
			while (true)
			{
				SurvivalFeelConfig config = resourceManager.SurvivalFeel;
				elapsed += Time.deltaTime;
				float ratio = Mathf.Clamp01(elapsed / config.PickupPullSeconds);
				float progress = config.PickupPullCurve.Evaluate(ratio);
				transform.position = playerProvider.Current.transform.position + startOffset * (1f - progress);
				if (ratio >= 1f)
				{
					Effect();
					_moveLoop = null;
					yield break;
				}
				yield return null;
			}
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

		protected virtual void OnDisable()
		{
			ObjectBufferManager.RemoveObject(ObjectType.Drop, gameObject);
		}
	}
}
