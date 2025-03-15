using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class BuildingObject : MonoBehaviour
	{
		public RuntimeBuildingData SaveData { get; private set; }

		public void Initialize(RuntimeBuildingData saveData)
		{
			SaveData = saveData;
		}
	}
}