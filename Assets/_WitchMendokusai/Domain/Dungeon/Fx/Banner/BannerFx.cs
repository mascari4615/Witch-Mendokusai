using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 띠 연출. context.Title 과 context.Subtitle 을 띠로 보여 주고 스스로 사라짐
	/// - 상자 없이 가운데 짙고 양 끝이 투명한 가로 띠 위에 큰 글자. 위에 작은 분류 글 (label), 띠 위아래로 가는 선과 마름모
	/// - 모양과 움직임은 Banner.uss 의 변형 클래스 (styleClass). 코드는 클래스를 켜는 시점만 정함
	/// - 진행은 실제 시간. 히트스톱과 결과창 정지 중에도 흐름
	/// - 같은 자리 (slot) 의 띠가 떠 있으면 앞의 것을 바로 치우고 새로 뜸
	/// - context.Seconds 가 0 보다 크면 머무는 시간으로 씀
	/// </summary>
	[CreateAssetMenu(fileName = "Banner_0_New", menuName = "WM/Fx/" + nameof(BannerFx))]
	public class BannerFx : CombatFx
	{
		private const string USS_ROOT = "wm-banner";
		private const string USS_IN = "wm-banner--in";
		private const string USS_OUT = "wm-banner--out";
		private const string USS_PULSE = "wm-banner--pulse";
		private const string USS_TINT = "wm-banner__tint";
		private const string USS_VIGNETTE = "wm-banner__vignette";
		private const string USS_GROUP = "wm-banner__group";
		private const string USS_ARCS = "wm-banner__arcs";
		private const string USS_ARC = "wm-banner__arc";
		private const string USS_ARC_NUMBER = "wm-banner__arc--";
		private const string USS_STREAK = "wm-banner__streak";
		private const string USS_SIDE = "wm-banner__side";
		private const string USS_SIDE_LEFT = "wm-banner__side--left";
		private const string USS_SIDE_RIGHT = "wm-banner__side--right";
		private const string USS_LABEL = "wm-banner__label";
		private const string USS_LABEL_EMPTY = "wm-banner__label--empty";
		private const string USS_RULE = "wm-banner__rule";
		private const string USS_RULE_TOP = "wm-banner__rule--top";
		private const string USS_RULE_BOTTOM = "wm-banner__rule--bottom";
		private const string USS_RULE_LINE = "wm-banner__rule-line";
		private const string USS_DIAMOND = "wm-banner__diamond";
		private const string USS_BAND = "wm-banner__band";
		private const string USS_TITLE = "wm-banner__title";
		private const string USS_CHARACTER = "wm-banner__char";
		private const string USS_CHARACTER_SPACE = "wm-banner__char--space";
		private const string USS_CHARACTER_SHOWN = "wm-banner__char--shown";
		private const string USS_SUBTITLE = "wm-banner__subtitle";
		private const string USS_SUBTITLE_EMPTY = "wm-banner__subtitle--empty";

		// 자리 이름별 지금 떠 있는 띠의 중단 함수
		private static readonly Dictionary<string, Action> active = new();

		[Header("_" + nameof(BannerFx))]
		[SerializeField, Tooltip("띠 모양 (Banner.uss)")] private StyleSheet styleSheet;
		[SerializeField, Tooltip("띠 뿌리에 붙는 변형 클래스. 예: wm-banner--warning")] private string styleClass = "wm-banner--warning";
		[SerializeField, Tooltip("제목 위 작은 분류 글. 예: 경고, 보스. 비우면 안 보임")] private string label = "";
		[SerializeField, Min(0), Tooltip("뒤에 까는 가는 원호 수. 원호 모양과 표시 여부는 USS (보스 변형만 보임)")] private int arcCount = 3;
		[SerializeField, Tooltip("겹침 판정 자리 이름. 같은 이름의 띠가 떠 있으면 앞의 것을 치움. 비우면 변형 클래스 이름")] private string slot = "";
		[SerializeField, Min(0f), Tooltip("등장에 주는 시간 (초). 글자가 이 안에 순서대로 나타남")] private float enterSeconds = 0.45f;
		[SerializeField, Min(0f), Tooltip("다 나타난 뒤 머무는 시간 (초)")] private float holdSeconds = 2.2f;
		[SerializeField, Min(0f), Tooltip("퇴장에 주는 시간 (초). 지나면 화면에서 제거")] private float exitSeconds = 0.3f;
		[SerializeField, Range(0f, 1f), Tooltip("첫 글자가 나타나는 시점. 등장 시간에 대한 비율")] private float titleStartPortion = 0.3f;
		[SerializeField, Range(0f, 1f), Tooltip("첫 글자부터 끝 글자까지 걸리는 시간. 등장 시간에 대한 비율")] private float titleSpreadPortion = 0.6f;
		[SerializeField, Tooltip("등장 때 낼 소리")] private UiSfxEvent sound = UiSfxEvent.Blocked;
		[SerializeField, Tooltip("등장 때 소리를 낼지")] private bool playSound = false;
		[SerializeField, Min(0), Tooltip("다 나타난 뒤 깜빡임 횟수. 0 이면 없음. 모양은 USS 의 --pulse")] private int pulseCount = 0;
		[SerializeField, Min(0f), Tooltip("깜빡임 한 번의 길이 (초)")] private float pulsePeriod = 0.4f;

		private string SlotKey => string.IsNullOrEmpty(slot) ? styleClass : slot;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState() => active.Clear();

		protected override Action Play(CombatFxRunner runner, CombatFxContext context)
		{
			string key = SlotKey;
			if (active.TryGetValue(key, out Action previous))
				previous.Invoke();

			List<VisualElement> characters = new();
			VisualElement root = Build(context, characters);
			UIRoot.Instance.OverlayLayer.Add(root);

			float hold = context.Seconds > 0f ? context.Seconds : holdSeconds;
			Action stop = null;
			stop = runner.Run(Animate(root, characters, hold), () =>
			{
				root.RemoveFromHierarchy();
				if (active.TryGetValue(key, out Action current) && current == stop)
					active.Remove(key);
			});
			active[key] = stop;

			if (playSound)
				UiSfx.Play(sound);
			return stop;
		}

		private VisualElement Build(CombatFxContext context, List<VisualElement> characters)
		{
			VisualElement root = Make(USS_ROOT);
			if (styleSheet == null)
				throw new InvalidOperationException($"{name}: styleSheet 가 비어 있음. 띠 모양 USS (Banner.uss) 를 연결해야 함");
			root.styleSheets.Add(styleSheet);
			if (string.IsNullOrEmpty(styleClass) == false)
				root.AddToClassList(styleClass);

			// 화면 전체 물들임과 가장자리 어둡게. 보이는지는 변형 클래스가 정함
			root.Add(Make(USS_TINT));
			root.Add(Make(USS_VIGNETTE));

			VisualElement group = Make(USS_GROUP);
			root.Add(group);

			VisualElement arcs = Make(USS_ARCS);
			for (int i = 0; i < arcCount; i++)
			{
				VisualElement arc = Make(USS_ARC);
				arc.AddToClassList(USS_ARC_NUMBER + (i + 1).ToString(CultureInfo.InvariantCulture));
				arcs.Add(arc);
			}
			group.Add(arcs);

			// 화면을 비스듬히 가로지르는 빛줄기. 승리 변형만 보임
			group.Add(Make(USS_STREAK));

			Label labelElement = new(label ?? string.Empty) { pickingMode = PickingMode.Ignore };
			labelElement.AddToClassList(USS_LABEL);
			labelElement.EnableInClassList(USS_LABEL_EMPTY, string.IsNullOrEmpty(label));
			group.Add(labelElement);

			group.Add(MakeRule(USS_RULE_TOP));

			VisualElement band = Make(USS_BAND);
			group.Add(band);

			VisualElement title = Make(USS_TITLE);
			band.Add(title);
			// 제목 양옆으로 뻗는 가는 선. 패배 변형만 보임
			VisualElement leftSide = Make(USS_SIDE);
			leftSide.AddToClassList(USS_SIDE_LEFT);
			title.Add(leftSide);
			TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(context.Title ?? string.Empty);
			while (enumerator.MoveNext())
			{
				string text = enumerator.GetTextElement();
				Label character = new(text) { pickingMode = PickingMode.Ignore };
				character.AddToClassList(USS_CHARACTER);
				if (string.IsNullOrWhiteSpace(text))
					character.AddToClassList(USS_CHARACTER_SPACE);
				title.Add(character);
				characters.Add(character);
			}
			VisualElement rightSide = Make(USS_SIDE);
			rightSide.AddToClassList(USS_SIDE_RIGHT);
			title.Add(rightSide);

			Label subtitle = new(context.Subtitle ?? string.Empty) { pickingMode = PickingMode.Ignore };
			subtitle.AddToClassList(USS_SUBTITLE);
			subtitle.EnableInClassList(USS_SUBTITLE_EMPTY, string.IsNullOrEmpty(context.Subtitle));
			band.Add(subtitle);

			group.Add(MakeRule(USS_RULE_BOTTOM));
			return root;
		}

		// 띠 위아래의 가는 선과 가운데 마름모
		private static VisualElement MakeRule(string positionClassName)
		{
			VisualElement rule = Make(USS_RULE);
			rule.AddToClassList(positionClassName);
			rule.Add(Make(USS_RULE_LINE));
			rule.Add(Make(USS_DIAMOND));
			return rule;
		}

		private static VisualElement Make(string className)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.AddToClassList(className);
			return element;
		}

		private IEnumerator Animate(VisualElement root, List<VisualElement> characters, float hold)
		{
			// 붙인 프레임에는 처음 모양이 아직 계산 전. 다음 프레임에 클래스를 켜야 transition 이 돎
			yield return null;
			root.AddToClassList(USS_IN);

			float firstCharacterAt = enterSeconds * titleStartPortion;
			float characterStep = characters.Count > 0 ? enterSeconds * titleSpreadPortion / characters.Count : 0f;
			float shownUntil = enterSeconds + hold;
			float elapsed = 0f;
			int shown = 0;
			while (elapsed < shownUntil)
			{
				while (shown < characters.Count && elapsed >= firstCharacterAt + characterStep * shown)
					characters[shown++].AddToClassList(USS_CHARACTER_SHOWN);
				if (pulseCount > 0 && pulsePeriod > 0f && elapsed >= enterSeconds)
				{
					int pulseIndex = Mathf.FloorToInt((elapsed - enterSeconds) / pulsePeriod * 2f);
					root.EnableInClassList(USS_PULSE, pulseIndex < pulseCount * 2 && pulseIndex % 2 == 0);
				}
				yield return null;
				elapsed += Time.unscaledDeltaTime;
			}

			root.AddToClassList(USS_OUT);
			yield return new WaitForSecondsRealtime(exitSeconds);
		}
	}
}
