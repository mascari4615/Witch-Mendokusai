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
		ResourceNode,
		Drop,
		Skill,
	}

	public class ObjectBufferManager
	{
		private static readonly Dictionary<ObjectType, List<GameObject>> bufferDic = new()
		{
			{ ObjectType.SpawnCircle, new List<GameObject>() },
			{ ObjectType.Monster, new List<GameObject>() },
			{ ObjectType.ResourceNode, new List<GameObject>() },
			{ ObjectType.Drop, new List<GameObject>() },
			{ ObjectType.Skill, new List<GameObject>() },
		};

		// domain reload disabled 시 static bufferDic 이 이전 Play 세션의 파괴된 오브젝트 참조를 유지함 → NullRef.
		// SubsystemRegistration = domain reload 유무와 무관하게 매 Play 시작 전 호출.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			foreach (ObjectType type in Enum.GetValues(typeof(ObjectType)))
				bufferDic[type].Clear();
		}

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