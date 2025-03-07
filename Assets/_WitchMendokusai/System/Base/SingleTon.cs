using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

namespace WitchMendokusai
{
	[DefaultExecutionOrder(-100)]
	public abstract class Singleton<T> : MonoBehaviour where T : Component
	{
		private static T instance;
		public static T Instance
		{
			get
			{
				if (instance)
					return instance;

				Object t = Resources.Load($"Singleton/{typeof(T).Name}");
				if (t != null)
				{
					instance = Instantiate(t).GetComponent<T>();
					return instance;
				}

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

				if (dontDestroyOnLoad)
					DontDestroyOnLoad(gameObject);
			}
			else
			{
				Destroy(gameObject);
			}
		}
	}
}