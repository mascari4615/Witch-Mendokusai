using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(SOManager), menuName = "WM/SOManager")]
	public class SOManager : ScriptableObject
	{
		public Dictionary<Type, Dictionary<int, DataSO>> DataSOs { get; private set; } = new();
		public Dictionary<int, DataSO> this[Type type]
		{
			get
			{
				if (DataSOs.TryGetValue(type, out var dataSOs))
					return dataSOs;

				return null;
			}
		}

		[field: Space(10), Header("PlayerData")]
		[field: SerializeField] public FloatVariable InvincibleTime { get; private set; }
		[field: SerializeField] public FloatVariable JoystickX { get; private set; }
		[field: SerializeField] public FloatVariable JoystickY { get; private set; }
		[NonSerialized] private static SOManager instance;
		public static SOManager Instance
		{
			get
			{
				if (instance == null)
					instance = Resources.Load(typeof(SOManager).Name) as SOManager;

				return instance;
			}
			private set => instance = value;
		}

		[field: Header("_" + nameof(SOManager))]
		[field: SerializeField] public FloatVariable DashDuration { get; private set; }
		[field: SerializeField] public FloatVariable DashSpeed { get; private set; }

		[field: SerializeField] public MonsterObjectVariable LastHitMonsterObject { get; private set; }
		[field: SerializeField] public ItemVariable LastEquippedItem { get; private set; }

		[field: Space(10), Header("Buffer")]
		[field: SerializeField] public QuestSOBuffer QuestDataBuffer { get; private set; }
		[field: SerializeField] public QuestBuffer QuestBuffer { get; private set; }
		[field: SerializeField] public DollBuffer DollBuffer { get; private set; }
		[field: SerializeField] public ItemDataBuffer ItemDataBuffer { get; private set; }
		[field: SerializeField] public Inventory ItemInventory { get; private set; }
		[field: SerializeField] public CardBuffer SelectedCardBuffer { get; private set; }
		[field: SerializeField] public QuestSO VQuestLoadQuest { get; private set; }
		[field: SerializeField] public QuestSOBuffer VQuests { get; private set; }
	}
}