using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 무기마다 이어진 적중 수를 공격자에게 보관. 타격 하나는 풀로 돌아가도 연속 기록은 이어짐
	public sealed class HitComboBook : MonoBehaviour
	{
		private readonly Dictionary<int, int> counts = new();

		public static HitComboBook Of(UnitObject owner)
		{
			if (owner.TryGetComponent(out HitComboBook book) == false)
				book = owner.gameObject.AddComponent<HitComboBook>();
			return book;
		}

		public int Count(int weaponId) => counts.TryGetValue(weaponId, out int count) ? count : 0;

		public void Hit(int weaponId, int maxCount) => counts[weaponId] = HitComboRules.AfterHit(Count(weaponId), maxCount);

		public void Miss(int weaponId) => counts[weaponId] = HitComboRules.AfterMiss();

		private void OnDisable() => counts.Clear();
	}
}
