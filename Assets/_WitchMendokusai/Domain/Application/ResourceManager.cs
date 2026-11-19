using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(ResourceManager), menuName = "WM/ResourceManager")]
	public class ResourceManager : ScriptableObject
	{
		[NonSerialized] private static ResourceManager instance;
		public static ResourceManager Instance
		{
			get
			{
				if (instance == null)
					instance = Resources.Load(typeof(ResourceManager).Name) as ResourceManager;

				return instance;
			}
			private set => instance = value;
		}

		[field: SerializeField] public GameObject EXPPrefab { get; private set; } = null;
		[field: SerializeField] public GameObject LootItemPrefab { get; private set; } = null;

		[field: SerializeField] public HealObject HealObjectPrefab { get; private set; } = null;
		[field: SerializeField] public MagnetObject MagnetObjectPrefab { get; private set; } = null;
		[field: SerializeField] public FreezeObject FreezeObjectPrefab { get; private set; } = null;
		[field: SerializeField] public ClearScreenObject ClearScreenObjectPrefab { get; private set; } = null;

		[field: SerializeField] public SurvivalFeelConfig SurvivalFeel { get; private set; } = null;
		[field: SerializeField] public SurvivalRunRules SurvivalRunRules { get; private set; } = null;
		[field: SerializeField, Tooltip("상태이상 규칙과 표시 (UnitStatusType 전부, 종류는 줄 추가로)")] public UnitStatusConfig UnitStatus { get; private set; } = null;
		[field: SerializeField, Tooltip("생존 모드 화면 알림 (정예, 무리, 보스 띠)")] public SurvivalAnnounceSettings SurvivalAnnounce { get; private set; } = null;
		[field: SerializeField, Tooltip("모든 유닛 능력치 시작값 (기본 치명타 확률과 피해)")] public UnitBaseStats UnitBaseStats { get; private set; } = null;
	}
}