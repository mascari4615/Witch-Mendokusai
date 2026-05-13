using System.Collections;
using UnityEngine;
using VContainer;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public abstract class LootObject : MonoBehaviour
	{
		[SerializeField] private float moveSpeed = 1f;
#pragma warning disable CS0414 // 베지어 커브 이동 미구현 예약 필드
		[SerializeField] private bool useBezierCurve = false;
#pragma warning restore CS0414

		private Coroutine _moveLoop;

		protected PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private void OnEnable()
		{
			StopAllCoroutines();
			_moveLoop = null;
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

			_moveLoop = StartCoroutine(MoveLoop());
		}

		private IEnumerator MoveLoop()
		{
			// for (float t = 0; t < 1; t += Time.deltaTime * moveSpeed)
			// {
			// 	transform.position = Vector3.Lerp(transform.position, playerProvider.Current.transform.position, t);

			while (true)
			{
				Vector3 direction = (playerProvider.Current.transform.position - transform.position).normalized;
				transform.position = transform.position + moveSpeed * Time.deltaTime * direction;

				if (Vector3.Distance(transform.position, playerProvider.Current.transform.position) < .3f)
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
