using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 슬라임 왕이 던지는 슬라임. 슬라임 발사와 직선 투척이 같이 씀
	/// - 날아가는 것은 몬스터 프리팹 그림만 빌린 가벼운 판 (실행기 Rent, Return)
	/// - 착지 순간 진짜 몬스터를 풀에서 꺼냄. 이 보스가 소환해 살아 있는 수가 상한 (LaunchMaxAlive) 이면 소환 안 함
	/// </summary>
	public sealed class SlimeKingSummons
	{
		private const string SHOT_KEY = "SlimeKingLaunchShot";

		private readonly SlimeKingAttackConfig config;
		private readonly List<MonsterObject> spawned = new();

		public SlimeKingSummons(SlimeKingAttackConfig config)
		{
			this.config = config;
		}

		/// <summary> 날아가는 판 하나를 빌려 던질 몬스터 그림을 입힘. 다 쓰면 ReturnShot </summary>
		public GameObject RentShot()
		{
			GameObject shot = CombatFxRunner.Instance.Rent(SHOT_KEY, CreateShot);
			DressShot(shot, config.LaunchMonster);
			return shot;
		}

		public void ReturnShot(GameObject shot) => CombatFxRunner.Instance.Return(SHOT_KEY, shot);

		/// <summary> 착지 자리에 슬라임 소환. 살아 있는 수가 상한이면 안 함 </summary>
		public void Summon(Vector3 landing)
		{
			Monster monster = config.LaunchMonster;
			if (monster == null || monster.Prefab == null)
				return;
			spawned.RemoveAll(IsGone);
			if (config.LaunchMaxAlive > 0 && spawned.Count >= config.LaunchMaxAlive)
				return;
			GameObject spawnedObject = ObjectPoolManager.Instance.Spawn(monster.Prefab);
			spawnedObject.transform.position = landing;
			MonsterObject spawnedMonster = spawnedObject.GetComponent<MonsterObject>();
			spawnedMonster.Init(monster);
			spawnedObject.SetActive(true);
			spawned.Add(spawnedMonster);
		}

		// 풀로 돌아갔거나 죽었으면 셈에서 뺌
		private static bool IsGone(MonsterObject monster) => monster == null || monster.gameObject.activeInHierarchy == false || monster.IsAlive == false;

		private static GameObject CreateShot()
		{
			GameObject shot = new(SHOT_KEY);
			SpriteRenderer shotRenderer = shot.AddComponent<SpriteRenderer>();
			shotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			return shot;
		}

		/// <summary> 몬스터 프리팹의 그림, 재질, 크기를 판에 옮김 </summary>
		private static void DressShot(GameObject shot, Monster monster)
		{
			SpriteRenderer shotRenderer = shot.GetComponent<SpriteRenderer>();
			if (monster == null || monster.Prefab == null)
			{
				shotRenderer.sprite = null;
				return;
			}
			SpriteRenderer source = monster.Prefab.GetComponentInChildren<SpriteRenderer>(true);
			shotRenderer.sprite = source != null ? source.sprite : null;
			shotRenderer.sharedMaterial = monster.Material != null ? monster.Material : (source != null ? source.sharedMaterial : shotRenderer.sharedMaterial);
			shot.transform.localScale = source != null ? source.transform.lossyScale : Vector3.one;
		}
	}
}
