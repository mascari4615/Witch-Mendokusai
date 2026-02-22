using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum ObjectType
	{
		SpawnCircle,
		Monster,
		Drop,
		Skill,
	}

	public class ObjectBufferManager
	{
		private static readonly Dictionary<ObjectType, List<GameObject>> bufferDic = new()
		{
			{ ObjectType.SpawnCircle, new List<GameObject>() },
			{ ObjectType.Monster, new List<GameObject>() },
			{ ObjectType.Drop, new List<GameObject>() },
			{ ObjectType.Skill, new List<GameObject>() },
		};

		public static void AddObject(ObjectType type, GameObject obj)
		{
			bufferDic[type].Add(obj);
		}

		public static void RemoveObject(ObjectType type, GameObject obj)
		{
			bufferDic[type].Remove(obj);
		}

		public static void ClearObjects(ObjectType type)
		{
			for (int i = bufferDic[type].Count - 1; i >= 0; i--)
			{
				GameObject obj = bufferDic[type][i];
				obj.SetActive(false);
			}

			bufferDic[type].Clear();
		}

		public static List<GameObject> GetObjects(ObjectType type)
		{
			return bufferDic[type];
		}

		public static List<GameObject> GetObjectsWithDistance(ObjectType type, Vector3 position, float maxDistance)
		{
			List<GameObject> targetObjects = new();

			foreach (GameObject obj in bufferDic[type])
			{
				float distance = Vector3.Distance(obj.transform.position, position);
				if (distance < maxDistance)
					targetObjects.Add(obj);
			}

			return targetObjects;
		}
	}
}