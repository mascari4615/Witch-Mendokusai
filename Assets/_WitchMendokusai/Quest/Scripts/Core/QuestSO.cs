using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "Q_", menuName = "Variable/" + nameof(QuestSO))]
	public class QuestSO : DataSO
	{
		[field: Header("_" + nameof(QuestSO))]
		[PropertyOrder(100)][field: SerializeField] public QuestInfo Data { get; private set; }
	}
}