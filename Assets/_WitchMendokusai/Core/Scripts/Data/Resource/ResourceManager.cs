using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(ResourceManager), menuName = "ResourceManager")]
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
	}
}