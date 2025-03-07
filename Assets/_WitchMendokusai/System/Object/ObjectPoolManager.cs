using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

		private string GetActualObjectName(GameObject targetObject)
		{
			// 프리팹 이름에서 "(Clone)"을 제거
			return targetObject.name.Contains("(Clone)")
				? targetObject.name.Remove(targetObject.name.IndexOf("(", StringComparison.Ordinal), 7)
				: targetObject.name;
		}
	}
}