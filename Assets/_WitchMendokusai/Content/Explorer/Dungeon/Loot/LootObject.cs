using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public abstract class LootObject : MonoBehaviour
	{
		[SerializeField] private float moveSpeed = 1f;
		[SerializeField] private bool useBezierCurve = false;

		private Coroutine _moveLoop;

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
			for (float t = 0; t < 1; t += Time.deltaTime * moveSpeed)
			{
				transform.position = Vector3.Lerp(transform.position, Player.Instance.transform.position, t);

				if (Vector3.Distance(transform.position, Player.Instance.transform.position) < .3f)
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
			if (IsPlaying)
				ObjectBufferManager.RemoveObject(ObjectType.Drop, gameObject);
		}
	}
}