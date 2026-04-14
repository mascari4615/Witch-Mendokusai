using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum ResourceNodeType
	{
		Mineral,
		Herb,
		Wood,
	}

	[CreateAssetMenu(fileName = nameof(ResourceNode), menuName = "WM/Variable/" + nameof(Unit) + "/" + nameof(ResourceNode))]
	public class ResourceNode : Unit
	{
		[field: Header("_" + nameof(ResourceNode))]
		[PropertyOrder(20)][field: SerializeField] public ResourceNodeType Type { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public EquipmentType RequiredTool { get; private set; }
		[PropertyOrder(22)][field: SerializeField] public List<DataSOWithPercentage> Loots { get; private set; }
	}
}