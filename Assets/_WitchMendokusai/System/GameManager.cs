using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class GameManager : Singleton<GameManager>
	{
		public bool IsChatting { get; set; }
		public bool IsDashing { get; set; }
		public bool IsCooling { get; set; }
		public bool IsDied { get; set; }
		public bool IsMouseOnUI { get; set; }

		// 게임 상태 초기화
		public void Init()
		{
			ObjectBufferManager.ClearObjects(ObjectType.Drop);
			ObjectBufferManager.ClearObjects(ObjectType.Monster);
			ObjectBufferManager.ClearObjects(ObjectType.Skill);
			ObjectBufferManager.ClearObjects(ObjectType.SpawnCircle);

			Player.Instance.Object.Init(GetDoll(DataManager.Instance.CurDollID));

			QuestManager.Instance.RemoveQuests(QuestType.Dungeon);
			DataManager.Instance.GameStat.UpdateData();
		}

		public void InitEquipment()
		{
			List<EquipmentData> equipments = DataManager.Instance.GetEquipmentDatas(DataManager.Instance.CurDollID);
			foreach (EquipmentData equipment in equipments)
			{
				if (equipment == null)
					continue;

				Effect.ApplyEffects(equipment.Effects);

				if (equipment.Object != null)
				{
					GameObject g = ObjectPoolManager.Instance.Spawn(equipment.Object);

					if (g.TryGetComponent(out SkillObject skillObject))
						skillObject.InitContext(Player.Instance.Object);

					g.SetActive(true);
				}
			}
		}
	}
}