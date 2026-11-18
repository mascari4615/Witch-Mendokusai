using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 풀에서 다시 쓰는 오브젝트의 구성 요소 찾기 결과 보관. 오브젝트마다 처음 한 번만 TryGetComponent
	// 없는 경우도 null 로 보관해 다시 찾지 않음. 작은 지도와 화면 밖 표시가 매 프레임 버퍼 목록을 훑을 때 사용
	public class ComponentLookup<T> where T : Component
	{
		private readonly Dictionary<GameObject, T> cache = new();

		public bool TryGet(GameObject target, out T component)
		{
			if (cache.TryGetValue(target, out component) == false)
			{
				target.TryGetComponent(out component);
				cache[target] = component;
			}
			return component != null;
		}

		public void Clear() => cache.Clear();
	}
}
