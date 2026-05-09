using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// spawn &lt;monsterRef&gt;
	/// monsterRef: ID 정수 / `M_&lt;num&gt;` / Name 부분.
	/// 플레이어 위치 근처 (반경 5) 랜덤 지점에 ObjectPoolManager 로 스폰.
	/// </summary>
	public class SpawnMonsterCommand : IDevCommand
	{
		public string Name => "spawn";
		public string Usage => "spawn <monsterRef>";

		private const string CODE_PREFIX = "M_";
		private const float SPAWN_RADIUS = 5f;

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}

			Monster target = DevDataLookup.Resolve<Monster>(args[0], CODE_PREFIX, out List<Monster> matches);
			if (target == null)
			{
				if (matches.Count == 0)
				{
					context.LogError($"매칭되는 몬스터 없음: '{args[0]}'");
					return;
				}
				context.LogWarn($"여러 후보 ({matches.Count}) — 더 정확히 입력:");
				for (int i = 0; i < matches.Count && i < 10; i++)
					context.LogWarn($"  {CODE_PREFIX}{matches[i].ID}  {matches[i].Name}");
				if (matches.Count > 10)
					context.LogWarn($"  ... 외 {matches.Count - 10}개");
				return;
			}

			if (target.Prefab == null)
			{
				context.LogError($"{target.Name} 의 Prefab 미할당");
				return;
			}

			Vector3 origin = PlayerProvider.Instance.Current != null ? PlayerProvider.Instance.Current.transform.position : Vector3.zero;
			Vector2 randomDir = Random.insideUnitCircle.normalized;
			Vector3 spawnPos = origin + new Vector3(randomDir.x, 0f, randomDir.y) * SPAWN_RADIUS;

			GameObject obj = ObjectPoolManager.Instance.Spawn(target.Prefab);
			obj.transform.position = spawnPos;
			obj.GetComponent<MonsterObject>().Init(target);
			obj.SetActive(true);

			context.LogSuccess($"{target.Name} 스폰 (ID {target.ID}, pos={spawnPos:F1})");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return System.Linq.Enumerable.Empty<string>();
			return DevDataLookup.SuggestRefs<Monster>(partial[0], CODE_PREFIX);
		}
	}
}
