using System;
using UnityEngine.UIElements;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// UXML 요소를 이름으로 찾되 없으면 그 자리에서 이름을 말하고 멈춤
	///
	/// 왜: `Q` 는 못 찾으면 null 을 주고, 그 null 은 한참 뒤 `clicked` 나 `text` 에서 NullReferenceException 으로 터짐
	/// (2026-09-01 실측: 배속과 AUTO 를 엉뚱한 페이지에서 찾아 플레이어 판이 NRE 다섯). 이름이 바로 나와야 UXML 을 고칠 수 있음
	/// 있어도 되고 없어도 되는 요소만 `Q` 그대로
	/// </summary>
	public static class UiQuery
	{
		public static T RequireQ<T>(this VisualElement root, string name) where T : VisualElement
		{
			T found = root.Q<T>(name);
			if (found == null)
			{
				throw new InvalidOperationException("UXML 에 '" + name + "' (" + typeof(T).Name + ") 이 없음. 찾은 뿌리 '" + root.name + "'. UXML 의 name 과 코드의 이름을 맞출 것");
			}

			return found;
		}
	}
}
