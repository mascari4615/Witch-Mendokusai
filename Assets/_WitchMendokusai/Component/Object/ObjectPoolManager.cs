using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class ObjectPoolManager : Singleton<ObjectPoolManager>
	{
		private readonly Dictionary<string, ObjectPool> poolDic = new();

		public void Despawn(GameObject targetObject)
		{
			if (targetObject == null)
				return;

			// Debug.Log($"PushObject: {targetObject.name}");
			string objectName = GetActualObjectName(targetObject);

			if (poolDic.ContainsKey(objectName) == false)
				poolDic[objectName] = new ObjectPool(targetObject);

			poolDic[objectName].Push(targetObject);
		}

		public GameObject Spawn(GameObject targetObject)
		{
			// Debug.Log($"PopObject: {targetObject.name}");
			string objectName = GetActualObjectName(targetObject);

			if (poolDic.TryGetValue(objectName, out ObjectPool pool))
			{
				return pool.Pop();
			}
			else
			{
				poolDic[objectName] = new ObjectPool(targetObject);
				poolDic[objectName].CreateObject(1);
				return poolDic[objectName].Pop();
			}
		}

		public GameObject Spawn(GameObject targetObject, Vector3 position, Quaternion rotation = default)
		{
			GameObject spawnedObject = Spawn(targetObject);
			spawnedObject.transform.SetPositionAndRotation(position, rotation);
			return spawnedObject;
		}

		public GameObject Spawn(GameObject targetObject, Transform parent, bool worldPositionStays = false)
		{
			GameObject spawnedObject = Spawn(targetObject);
			spawnedObject.transform.SetParent(parent, worldPositionStays);
			spawnedObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			return spawnedObject;
		}

		private static string GetActualObjectName(GameObject targetObject)
		{
			// 프리팹 이름에서 "(Clone)"을 제거
			return targetObject.name.Contains("(Clone)")
				? targetObject.name.Remove(targetObject.name.IndexOf("(", StringComparison.Ordinal), 7)
				: targetObject.name;
		}

		// ObjectPool
		private class ObjectPool
		{
			private static readonly Dictionary<ObjectPool, Transform> ObjectParent = new();

			private readonly GameObject prefab;
			private readonly Stack<GameObject> stack;

			public ObjectPool(GameObject prefab)
			{
				this.prefab = prefab;
				stack = new();
			}

			public void CreateObject(int count = 1)
			{
				for (int i = 0; i < count; i++)
				{
					GameObject g = Instantiate(prefab, GetObjectParent(this));
					g.SetActive(false);
					Push(g);
				}
			}

			public void Push(GameObject targetObject)
			{
				if (stack.Contains(targetObject))
				{
					// Debug.Log($"{targetObject.name}, 이미 스택에 존재합니다");
					return;
				}

				if (targetObject.activeSelf)
				{
					targetObject.SetActive(false);
				}
				stack.Push(targetObject);

				// 활성화/비활성화 이후, 부모 오브젝트를 변경하기 위해 1프레임 대기 - 2025.03.19 22:23
				UniTask.DelayFrame(1).ContinueWith(() =>
				{
					// 그 사이에 stack에서 뽑힌 경우를 확인
					if (stack.Contains(targetObject) == false)
					{
						return;
					}

					// Editor Timed에서 PlayMode 중지 시 Error Log 발생하는 것을 방지 - 2025.03.19 22:23
					if (IsPlaying == false)
					{
						return;
					}

					if (targetObject.transform.parent != GetObjectParent(this))
					{
						targetObject.transform.SetParent(GetObjectParent(this));
					}
				}).Forget();
			}

			public GameObject Pop()
			{
				if (stack.Count == 0)
				{
					CreateObject(5);
				}

				GameObject o = stack.Pop();
				// o.SetActive(true);

				return o;
			}

			private static Transform GetObjectParent(ObjectPool objectPool)
			{
				if (ObjectParent.ContainsKey(objectPool) == false)
				{
					GameObject poolParentObject = new($"[{nameof(ObjectPool)}] {objectPool.prefab.name}");
					poolParentObject.transform.SetParent(Instance.transform);

					ObjectParent[objectPool] = poolParentObject.transform;
				}

				return ObjectParent[objectPool];
			}
		}
	}
}