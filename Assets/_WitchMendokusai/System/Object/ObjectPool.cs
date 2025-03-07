using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class ObjectPool
	{
		private readonly GameObject prefab;
		private readonly Stack<GameObject> stack;

		public ObjectPool(GameObject prefab)
		{
			this.prefab = prefab;
			stack = new();
		}

		public void CreateObject(int count = 1)
		{
			for (var i = 0; i < count; i++)
			{
				GameObject g = UnityEngine.Object.Instantiate(prefab);
				g.SetActive(false);
				Push(g);
			}
		}

		public void Push(GameObject targetObject)
		{
			if (targetObject.activeSelf)
				targetObject.SetActive(false);

			if (stack.Contains(targetObject))
			{
				// Debug.Log($"{targetObject.name}, 이미 스택에 존재합니다");
				return;
			}

			stack.Push(targetObject);
		}

		public GameObject Pop()
		{
			if (stack.Count == 0)
				CreateObject(5);

			GameObject o = stack.Pop();
			// o.SetActive(true);

			return o;
		}
	}
}