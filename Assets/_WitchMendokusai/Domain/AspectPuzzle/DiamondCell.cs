using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>번갈아 판의 칸. 스킬 트리의 마름모 (SkillTree.uss) 를 그대로 쓰고 상태 색만 AspectPuzzle.uss</summary>
	public sealed class DiamondCell : AspectCell
	{
		private const string USS_CLASS = "wm-aspect-diamond";
		private const string NODE_CLASS = "wm-skilltree__node";
		private const string FOCUSED_CLASS = NODE_CLASS + "--focused";

		public DiamondCell(Action submitted) : base(submitted)
		{
			AddToClassList(NODE_CLASS);
			AddToClassList(USS_CLASS);
			Part(this, "wm-skilltree__focus");
			VisualElement diamond = Part(this, "wm-skilltree__diamond");
			Part(diamond, "wm-skilltree__diamond-line");
			AddMarks();
			RegisterCallback<FocusEvent>(_ => AddToClassList(FOCUSED_CLASS));
			RegisterCallback<BlurEvent>(_ => RemoveFromClassList(FOCUSED_CLASS));
		}

		public override bool ContainsPoint(Vector2 localPoint)
		{
			Vector2 fromCenter = localPoint - contentRect.center;
			return Mathf.Abs(fromCenter.x) + Mathf.Abs(fromCenter.y) <= contentRect.width * 0.5f;
		}

		private static VisualElement Part(VisualElement parent, string className)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.AddToClassList(className);
			parent.Add(element);
			return element;
		}
	}
}
