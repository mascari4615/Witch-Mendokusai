using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WitchMendokusai
{
	public static class WMHelper
	{
		public static bool IsPlaying => IsPlaying_();
		private static bool IsPlaying_()
		{
#if UNITY_EDITOR
			return EditorApplication.isPlayingOrWillChangePlaymode;
#else
			return Application.isPlaying;
#endif
		}

		#region GetNearest
		public static T GetNearest<T>(List<T> list, Vector3 targetPosition, float maxDistance) where T : MonoBehaviour
		{
			return GetNearest(list, element => element.transform.position, targetPosition, maxDistance);
		}

		public static T GetNearest<T>(List<T> list, Func<T, Vector3> getPositionByElement, Vector3 targetPosition, float maxDistance, bool ignoreInactive = true) where T : MonoBehaviour
		{
			T nearest = default;
			float nearestDistance = float.MaxValue;

			foreach (T element in list)
			{
				if (ignoreInactive && (element.gameObject.activeInHierarchy == false))
					continue;

				Vector3 elementPosition = getPositionByElement(element);
				float distance = Vector3.Distance(elementPosition, targetPosition);

				if (distance < nearestDistance)
				{
					nearest = element;
					nearestDistance = distance;
				}
			}

			return nearestDistance <= maxDistance ? nearest : default;
		}
		#endregion
	}
}