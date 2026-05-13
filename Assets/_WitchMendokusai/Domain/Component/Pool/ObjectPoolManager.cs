using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using static WitchMendokusai.WMHelper;

namespace WitchMendokusai
{
	public class ObjectPoolManager : MonoBehaviour
	{
		public static ObjectPoolManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out ObjectPoolManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private IObjectResolver container;
		private readonly Dictionary<string, ObjectPool> poolDic = new();

		[Inject]
		public void Construct(IObjectResolver container)
		{
			this.container = container;
			ObjectPoolManagerBridge.Register(this);
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public void Despawn(GameObject targetObject)
		{
			if (targetObject == null)
				return;

			string objectName = GetActualObjectName(targetObject);

			if (poolDic.ContainsKey(objectName) == false)
				poolDic[objectName] = new ObjectPool(targetObject, container, transform);

			poolDic[objectName].Push(targetObject);
		}

		public GameObject Spawn(GameObject targetObject)
		{
			string objectName = GetActualObjectName(targetObject);

			if (poolDic.TryGetValue(objectName, out ObjectPool pool))
			{
				return pool.Pop();
			}
			else
			{
				poolDic[objectName] = new ObjectPool(targetObject, container, transform);
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
			private readonly IObjectResolver container;
			private readonly Transform managerTransform;

			public ObjectPool(GameObject prefab, IObjectResolver container, Transform managerTransform)
			{
				this.prefab = prefab;
				this.container = container;
				this.managerTransform = managerTransform;
				stack = new();
			}

			public void CreateObject(int count = 1)
			{
				// Instantiate가 활성 상태로 생성되면 OnEnable이 풀의 임시 부모 아래에서 트리거된다.
				// 자식이 GetComponentInParent 등 부모 의존 초기화를 하면 잘못된 결과를 얻으므로,
				// prefab을 비활성으로 토글한 채 Instantiate해 OnEnable 호출 자체를 막는다.
				bool wasActive = prefab.activeSelf;
				if (wasActive)
					prefab.SetActive(false);

				for (int i = 0; i < count; i++)
				{
					GameObject g = Instantiate(prefab, GetObjectParent(this));
					foreach (MonoBehaviour component in g.GetComponentsInChildren<MonoBehaviour>(true))
						container.Inject(component);
					Push(g);
				}

				if (wasActive)
					prefab.SetActive(true);
			}

			public void Push(GameObject targetObject)
			{
				if (stack.Contains(targetObject))
				{
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
					poolParentObject.transform.SetParent(objectPool.managerTransform);

					ObjectParent[objectPool] = poolParentObject.transform;
				}

				return ObjectParent[objectPool];
			}
		}
	}
}
