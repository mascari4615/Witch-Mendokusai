using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class Unit : DataSO
	{
		[field: Header("_" + nameof(Unit))]
		[PropertyOrder(10)][field: SerializeField] public UnitAffiliation Affiliation { get; set; } = UnitAffiliation.None;

		[PropertyOrder(20)][field: SerializeField] public GameObject Prefab { get; set; }
		[PropertyOrder(21)][field: SerializeField] public SkillData[] DefaultSkills { get; set; }
		[PropertyOrder(22)][field: SerializeField] public Material Material { get; set; }
		[PropertyOrder(23)][field: SerializeField] public UnitStatInfos InitStatInfos { get; set; }
	}
}