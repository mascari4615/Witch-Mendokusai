using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 전투 연출 부품의 실행기. 에셋 (ScriptableObject) 은 코루틴과 오브젝트를 가질 수 없어서 여기서 돌림
	/// - 씬마다 하나, 처음 쓸 때 만들어짐
	/// - Run: 코루틴 실행, 돌려받은 함수로 중단
	/// - Rent, Return: 부품이 코드로 만든 오브젝트 (예고 판, 띠) 를 종류별로 재사용. 프리팹은 ObjectPoolManager
	/// </summary>
	public class CombatFxRunner : MonoBehaviour
	{
		private static CombatFxRunner instance;

		public static CombatFxRunner Instance
		{
			get
			{
				if (instance == null)
				{
					GameObject host = new("CombatFxRunner");
					instance = host.AddComponent<CombatFxRunner>();
				}
				return instance;
			}
		}

		private readonly Dictionary<string, Stack<GameObject>> idle = new();

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;
		}

		/// <summary> 코루틴을 돌리고 중단 함수를 돌려줌. onStop 은 끝났든 중단됐든 한 번만 불림 (오브젝트 반납 자리) </summary>
		public Action Run(IEnumerator routine, Action onStop = null)
		{
			bool finished = false;
			Coroutine coroutine = null;

			void Finish()
			{
				if (finished)
					return;
				finished = true;
				if (coroutine != null && this != null)
					StopCoroutine(coroutine);
				onStop?.Invoke();
			}

			IEnumerator Wrapped()
			{
				yield return routine;
				coroutine = null;
				Finish();
			}

			coroutine = StartCoroutine(Wrapped());
			return Finish;
		}

		/// <summary> key 종류의 쉬는 오브젝트를 꺼내고, 없으면 create 로 만듦. 꺼낸 것은 켜진 상태 </summary>
		public GameObject Rent(string key, Func<GameObject> create)
		{
			GameObject rented = null;
			if (idle.TryGetValue(key, out Stack<GameObject> stack))
			{
				while (stack.Count > 0 && rented == null)
					rented = stack.Pop();
			}

			if (rented == null)
			{
				rented = create();
				rented.transform.SetParent(transform, false);
			}
			rented.SetActive(true);
			return rented;
		}

		public void Return(string key, GameObject rented)
		{
			if (rented == null)
				return;
			rented.SetActive(false);
			if (idle.TryGetValue(key, out Stack<GameObject> stack) == false)
			{
				stack = new Stack<GameObject>();
				idle[key] = stack;
			}
			stack.Push(rented);
		}
	}
}
