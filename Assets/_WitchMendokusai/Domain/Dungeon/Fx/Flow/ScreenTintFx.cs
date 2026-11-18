using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 전체를 한 색으로 덮는 연출. 번쩍임 (밝은 색이 빠르게 떴다 사라짐) 과 물들임 (색이 천천히 깔림)
	/// - 떠 있는 층의 맨 아래에 깔려서 띠와 화면 전환보다 뒤에 그려짐. 입력은 통과
	/// - 실제 시간. 멈춤 함수를 부르면 나가는 시간에 걸쳐 걷음
	/// </summary>
	[CreateAssetMenu(fileName = "Tint_0_New", menuName = "WM/CombatFx/" + nameof(ScreenTintFx))]
	public class ScreenTintFx : CombatFx
	{
		private const string ELEMENT_NAME = "screen-tint-fx";
		private const string VEIL_CLASS = "wm-screen-tint";

		[SerializeField, Tooltip("막 모양 (ScreenTint.uss). 위치와 크기를 정함")] private StyleSheet styleSheet;
		[SerializeField, Tooltip("덮는 색. 알파는 아래 최대 불투명도가 정함")] private Color color = Color.white;
		[SerializeField, Range(0f, 1f), Tooltip("가장 짙을 때 불투명도")] private float maxAlpha = 0.5f;
		[SerializeField, Tooltip("들어오는 시간, 머무는 시간, 나가는 시간, 곡선")] private FlowEnvelope envelope = new();
		[SerializeField, Min(1), Tooltip("실행기가 사라졌는지 살피는 간격 (밀리초). 씬이 내려가 걷는 연출이 끊기면 막을 바로 치움")] private int orphanCheckMilliseconds = 100;

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			if (styleSheet == null)
				throw new InvalidOperationException($"{name}: styleSheet 가 비어 있음. 막 모양 USS (ScreenTint.uss) 를 연결해야 함");

			VisualElement veil = new() { name = ELEMENT_NAME, pickingMode = PickingMode.Ignore };
			veil.styleSheets.Add(styleSheet);
			veil.AddToClassList(VEIL_CLASS);
			// 색은 에셋 값이고 시간에 따라 바뀌는 투명도라 코드가 정함. 위치와 크기는 USS
			veil.style.backgroundColor = new Color(color.r, color.g, color.b, 0f);

			VisualElement layer = UIRoot.Instance.OverlayLayer;
			layer.Add(veil);
			veil.SendToBack();
			// 실행기가 먼저 파괴되면 걷는 코루틴이 죽어 막이 남음. 화면 쪽 스케줄러로 따로 살핌
			veil.schedule.Execute(() =>
			{
				if (runner == null)
					veil.RemoveFromHierarchy();
			}).Every(orphanCheckMilliseconds);

			return envelope.Play(
				runner,
				weight => veil.style.backgroundColor = new Color(color.r, color.g, color.b, maxAlpha * Mathf.Clamp01(weight)),
				veil.RemoveFromHierarchy);
		}
	}
}
