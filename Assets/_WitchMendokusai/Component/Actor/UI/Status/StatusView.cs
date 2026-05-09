using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class StatusView : MonoBehaviour
	{
		private const string WINDOW_ID = "Status";

		private static readonly UnitStatType[] DISPLAYED_STATS = new[]
		{
			UnitStatType.EXP_BONUS,
			UnitStatType.MOVEMENT_SPEED,
			UnitStatType.PICKUP_RADIUS,
			UnitStatType.COOLTIME_BONUS,
			UnitStatType.ATTACK_SPEED_BONUS,
			UnitStatType.PROJECTILE_COUNT_BONUS,
			UnitStatType.PROJECTILE_SPEED_BONUS,
			UnitStatType.PROJECTILE_DURATION_BONUS,
			UnitStatType.PROJECTILE_SCALE_BONUS,
			UnitStatType.PROJECTILE_PIERCE_BONUS,
			UnitStatType.DAMAGE_BONUS,
			UnitStatType.CRITICAL_CHANCE,
			UnitStatType.CRITICAL_DAMAGE,
			UnitStatType.ARMOR,
			UnitStatType.DODGE,
			UnitStatType.INVINCIBLE_TIME,
			UnitStatType.GOLD_BONUS,
		};

		private WMWindow window;
		private readonly List<StatRow> rows = new();

		private void Start()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "스탯"
			};
			window.style.left = 320;
			window.style.top = 100;
			window.style.width = 320;
			window.style.height = 480;
			UIRoot.Instance.WindowsLayer.Add(window);

			ScrollView scrollView = new();
			window.Content.Add(scrollView);

			foreach (UnitStatType type in DISPLAYED_STATS)
			{
				StatRow row = new(type);
				scrollView.Add(row);
				rows.Add(row);
			}

			InputManager.Instance.RegisterInputEvent(InputEventType.Status, InputEventResponseType.Performed, OnToggle);
			TimeManager.Instance.RegisterCallback(Refresh);
		}

		private void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.Status, InputEventResponseType.Performed, OnToggle);
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.RemoveCallback(Refresh);
		}

		private void Refresh()
		{
			if (window == null || window.IsOpen == false)
				return;
			if (PlayerProvider.Instance.Current == null)
				return;

			foreach (StatRow row in rows)
				row.Refresh(PlayerProvider.Instance.Current.UnitStat);
		}

		private void OnToggle()
		{
			if (window == null)
				return;
			window.Toggle();
			if (window.IsOpen)
				Refresh();
		}
	}
}
