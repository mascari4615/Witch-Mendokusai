using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 풀에서 다시 쓰는 오브젝트의 구성 요소 찾기 결과 보관. 오브젝트마다 처음 한 번만 TryGetComponent
	// 없는 경우도 null 로 보관해 다시 찾지 않음. 작은 지도와 화면 밖 표시가 매 프레임 버퍼 목록을 훑을 때 사용
	public class ComponentLookup<T> where T : Component
	{
		private readonly Dictionary<GameObject, T> cache = new();
		private readonly List<GameObject> destroyedKeys = new();
		// 마지막 정리 뒤 항목 수. 두 배로 늘면 파괴된 키 정리 (새 항목 추가 비용에 나눠 냄)
		private int countAfterPrune;

		public bool TryGet(GameObject target, out T component)
		{
			if (cache.TryGetValue(target, out component) == false)
			{
				if (cache.Count >= countAfterPrune * 2)
					PruneDestroyed();
				target.TryGetComponent(out component);
				cache[target] = component;
			}
			return component != null;
		}

		// 파괴된 GameObject 키 제거
		public void PruneDestroyed()
		{
			destroyedKeys.Clear();
			foreach (GameObject key in cache.Keys)
			{
				if (key == null)
					destroyedKeys.Add(key);
			}
			for (int i = 0; i < destroyedKeys.Count; i++)
				cache.Remove(destroyedKeys[i]);
			destroyedKeys.Clear();
			countAfterPrune = cache.Count;
		}

		public void Clear()
		{
			cache.Clear();
			destroyedKeys.Clear();
			countAfterPrune = 0;
		}
	}
}
