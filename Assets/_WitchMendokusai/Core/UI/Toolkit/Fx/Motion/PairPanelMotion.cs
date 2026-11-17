using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>열기와 닫기를 다른 에셋으로. 예: 열기 Step, 닫기 Round</summary>
	[CreateAssetMenu(fileName = "Motion_0_Pair", menuName = "WM/UI/Panel Motion/Pair")]
	public sealed class PairPanelMotion : UIPanelMotion
	{
		public UIPanelMotion open;
		public UIPanelMotion close;

		protected override Action Open(VisualElement panel, VisualElement source, Action onDone) => Part(open).OpenPart(panel, source, onDone);

		protected override Action Close(VisualElement panel, VisualElement source, Action onDone) => Part(close).ClosePart(panel, source, onDone);

		private UIPanelMotion Part(UIPanelMotion motion)
		{
			if (motion == null || motion is PairPanelMotion || motion is UssPanelMotion)
				throw new InvalidOperationException($"{name}: 열기, 닫기 칸이 비었거나 묶음, USS 움직임이 들어 있음 (USS 는 display 를 스스로 다뤄 짝으로 못 씀)");
			return motion;
		}
	}
}
