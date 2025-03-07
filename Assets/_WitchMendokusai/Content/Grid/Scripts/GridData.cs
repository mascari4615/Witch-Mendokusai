using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class GridData
	{
		private readonly Dictionary<Vector3Int, GameObject> placedObjects = new();

		public bool TryGetObjectAt(Vector3Int gridPosition, out GameObject gameObject)
		{
			return placedObjects.TryGetValue(gridPosition, out gameObject);
		}

		public void AddObjectAt(Vector3Int gridPosition, GameObject gameObject)
		{
			if (placedObjects.ContainsKey(gridPosition))
				return;
		
			placedObjects[gridPosition] = gameObject;
		}

		public void RemoveObjectAt(Vector3Int gridPosition)
		{
			if (placedObjects.ContainsKey(gridPosition) == false)
				return;

			placedObjects[gridPosition].SetActive(false);
			placedObjects.Remove(gridPosition);
		}
	}
}