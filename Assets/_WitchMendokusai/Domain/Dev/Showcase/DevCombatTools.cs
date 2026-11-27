using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 개발자 창과 진열장이 같이 쓰는 전투 조작. 상태이상 걸기, 무기 하나만 들기
	public static class DevCombatTools
	{
		// 상태이상 대상 범위
		public enum StatusScope
		{
			Nearest = 0,
			Screen = 1,
		}

		// 걸린 몬스터 수. seconds 0 이하면 상태이상 설정의 기본 지속
		public static int ApplyStatus(UnitStatusType type, StatusScope scope, float seconds)
		{
			PlayerObject player = PlayerProvider.Instance.CurrentObject;
			List<MonsterObject> targets = FindTargets(player, scope);
			foreach (MonsterObject target in targets)
			{
				if (seconds > 0f)
					UnitStatus.Apply(target, type, player, seconds);
				else
					UnitStatus.Apply(target, type, player);
			}
			return targets.Count;
		}

		private static List<MonsterObject> FindTargets(PlayerObject player, StatusScope scope)
		{
			List<MonsterObject> targets = new();
			Camera camera = Camera.main;
			MonsterObject nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (GameObject monsterObject in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (monsterObject == null || monsterObject.activeInHierarchy == false)
					continue;
				MonsterObject monster = monsterObject.GetComponent<MonsterObject>();
				if (monster == null || monster.IsAlive == false)
					continue;

				if (scope == StatusScope.Screen)
				{
					if (camera != null && IsOnScreen(camera, monster.transform.position))
						targets.Add(monster);
					continue;
				}

				float distance = (monster.transform.position - player.transform.position).sqrMagnitude;
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearest = monster;
				}
			}

			if (scope == StatusScope.Nearest && nearest != null)
				targets.Add(nearest);
			return targets;
		}

		private static bool IsOnScreen(Camera camera, Vector3 worldPosition)
		{
			Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
			return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
		}

		// 지금 든 무기 동작을 전부 걷고 이 무기 하나만. null 이면 맨손
		// 장비의 능력치 효과는 넣지 않음. 같은 무기를 여러 번 들어도 능력치가 쌓이지 않게
		public static void EquipOnly(EquipmentData equipment)
		{
			ObjectBufferManager.ClearObjects(ObjectType.Skill);
			if (equipment == null)
				return;
			if (equipment.Object == null)
				throw new System.InvalidOperationException($"{equipment.Name} 동작 오브젝트 없음");

			PlayerProvider playerProvider = PlayerProvider.Instance;
			ObjectPoolManager objectPoolManager = ObjectPoolManager.Instance;
			GameObject weapon = objectPoolManager.Spawn(equipment.Object);
			if (weapon.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager, equipment));
			weapon.SetActive(true);
		}

		// 새 동작 부품 시험용 무기 (장비 데이터 없음, 아이템 목록에 안 올림). Resources/DevTrialWeapons
		public const string TRIAL_RESOURCE_PATH = "DevTrialWeapons";

		public static GameObject[] TrialWeapons()
		{
			GameObject[] weapons = Resources.LoadAll<GameObject>(TRIAL_RESOURCE_PATH);
			System.Array.Sort(weapons, (left, right) => string.CompareOrdinal(left.name, right.name));
			return weapons;
		}

		// EquipOnly 와 같은 길로 시험 무기 하나만 듦
		public static void EquipTrialOnly(GameObject weaponObject)
		{
			ObjectBufferManager.ClearObjects(ObjectType.Skill);
			PlayerProvider playerProvider = PlayerProvider.Instance;
			ObjectPoolManager objectPoolManager = ObjectPoolManager.Instance;
			GameObject weapon = objectPoolManager.Spawn(weaponObject);
			if (weapon.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager, DevTrialEquipment.Instance));
			weapon.SetActive(true);
		}

		// 진열과 무기 명령 후보. 동작 오브젝트가 있는 장비
		public static List<EquipmentData> AllWeapons()
		{
			List<EquipmentData> weapons = new();
			SOHelper.ForEach<ItemData>(itemData =>
			{
				if (itemData is EquipmentData equipment && equipment.Object != null)
					weapons.Add(equipment);
			});
			weapons.Sort((left, right) => left.ID.CompareTo(right.ID));
			return weapons;
		}
	}
}
