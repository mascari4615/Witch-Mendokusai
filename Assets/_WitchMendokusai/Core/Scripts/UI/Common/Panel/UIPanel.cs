using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class UIPanel : UIBase
	{
		[field: Header("_" + nameof(UIPanel))]
		[field: SerializeField] public string Name { get; private set; } = "UIPanel";
		[field: SerializeField] public Sprite PanelIcon { get; private set; } = null;
	}
}