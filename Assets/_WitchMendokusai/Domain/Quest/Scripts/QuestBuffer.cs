using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(QuestBuffer), menuName = "WM/DataBuffer/" + nameof(RuntimeQuest))]
	public class QuestBuffer : DataBufferSO<RuntimeQuest> { }
}