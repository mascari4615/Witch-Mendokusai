using UnityEngine;

namespace WitchMendokusai
{
	[DefaultExecutionOrder(-100)]
	public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
	{
		private static T instance;
		public static T Instance
		{
			get
			{
				if (instance != null)
					return instance;

				T prefab = Resources.Load<T>($"Singletons/{typeof(T).Name}");
				if (prefab != null)
				{
					instance = Instantiate(prefab);
					return instance;
				}

				T[] prefabs = Resources.FindObjectsOfTypeAll<T>();
				if (prefabs.Length > 0)
				{
					instance = Instantiate(prefabs[0]);
					return instance;
				}

				Debug.LogError($"There is no {typeof(T).Name} in Resources folder.");
				return null;
			}
			private set => instance = value;
		}

		[SerializeField] private bool dontDestroyOnLoad = false;

		protected virtual void Awake()
		{
			if (instance == null)
			{
				instance = this as T;

				if (dontDestroyOnLoad == true)
					DontDestroyOnLoad(gameObject);
			}
			else if (instance == this)
			{
				if (dontDestroyOnLoad == true)
					DontDestroyOnLoad(gameObject);
			}
			else
			{
				if (instance != this)
					Destroy(gameObject);
			}
		}

		protected virtual void OnDestroy()
		{
			if (instance == this)
			{
				if (WMHelper.IsPlaying)
					Debug.Log($"Destroy {typeof(T).Name}. {instance.name}");
				instance = null;
			}
		}
	}
}