using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Tests
{
	// 스킬창 장비 칸 대기 표시 (사용자 2026-11-21 플레이 피드백)
	public class RunEquipmentHudCooldownTests
	{
		private const float NUMBER_MIN_INTERVAL = 1.5f;
		private const int FLASH_HOLD_MILLISECONDS = 60;
		private const string SUBSECOND_FORMAT = "0.0";

		private EquipmentData cooling;
		private EquipmentData always;
		private FakeCooldown source;
		private RunEquipmentHud hud;

		[SetUp]
		public void SetUp()
		{
			cooling = ScriptableObject.CreateInstance<EquipmentData>();
			always = ScriptableObject.CreateInstance<EquipmentData>();
			cooling.ID = 990001;
			always.ID = 990002;
			source = new FakeCooldown();
			EquipmentCooldowns.Register(cooling, source);
			hud = new RunEquipmentHud();
			hud.SetCooldownDisplay(NUMBER_MIN_INTERVAL, FLASH_HOLD_MILLISECONDS, SUBSECOND_FORMAT);
		}

		[TearDown]
		public void TearDown()
		{
			EquipmentCooldowns.Unregister(cooling, source);
			Object.DestroyImmediate(cooling);
			Object.DestroyImmediate(always);
		}

		[Test]
		public void Cooling_equipment_shows_seconds_and_unregistered_shows_always()
		{
			Refresh(new CooldownState(0.5f, 2.4f, 4.8f));

			Assert.IsFalse(Remain(0).ClassListContains(RunEquipmentHud.USS_REMAIN_EMPTY));
			Assert.AreEqual("3", Remain(0).text);
			Assert.IsTrue(State(0).ClassListContains(RunEquipmentHud.USS_STATE_EMPTY));

			Assert.IsTrue(Remain(1).ClassListContains(RunEquipmentHud.USS_REMAIN_EMPTY));
			Assert.IsFalse(State(1).ClassListContains(RunEquipmentHud.USS_STATE_EMPTY));
		}

		[Test]
		public void Under_one_second_uses_subsecond_format()
		{
			Refresh(new CooldownState(0.1f, 0.48f, 4.8f));
			Assert.AreEqual("0.5", Remain(0).text);
		}

		[Test]
		public void Reaching_zero_hides_number_and_flashes()
		{
			Refresh(new CooldownState(0.5f, 2.4f, 4.8f));
			Refresh(new CooldownState(0f, 0f, 4.8f));
			Assert.IsTrue(Remain(0).ClassListContains(RunEquipmentHud.USS_REMAIN_EMPTY));
			Assert.IsTrue(IsFlashing(0));
		}

		// 주기형 무기는 준비된 프레임에 다음 주기를 잡아 0 을 거의 못 봄. 비율이 다시 커지면 준비로 봄
		[Test]
		public void Cycle_restart_between_refreshes_flashes()
		{
			Refresh(new CooldownState(0.03f, 0.14f, 4.8f));
			Refresh(new CooldownState(0.99f, 4.75f, 4.8f));
			Assert.IsTrue(IsFlashing(0));
		}

		[Test]
		public void Short_interval_shows_overlay_only()
		{
			Refresh(new CooldownState(0.5f, 0.5f, 1f));
			Assert.IsTrue(Remain(0).ClassListContains(RunEquipmentHud.USS_REMAIN_EMPTY));
			Refresh(new CooldownState(0.98f, 0.98f, 1f));
			Assert.IsFalse(IsFlashing(0));
		}

		[Test]
		public void Charge_has_no_number_and_flashes_when_full()
		{
			Refresh(CooldownState.FromCharge(0.6f));
			Assert.IsTrue(Remain(0).ClassListContains(RunEquipmentHud.USS_REMAIN_EMPTY));
			Refresh(CooldownState.FromCharge(1f));
			Assert.IsTrue(IsFlashing(0));
		}

		[Test]
		public void New_run_does_not_flash_from_last_run_state()
		{
			Refresh(new CooldownState(0.5f, 2.4f, 4.8f));
			hud.ResetShown();
			Refresh(new CooldownState(0.9f, 4.3f, 4.8f));
			Assert.IsFalse(IsFlashing(0));
		}

		private void Refresh(CooldownState state)
		{
			source.Cooldown = state;
			hud.RefreshEquipments(new[] { cooling, always }, new[] { 1, 1 }, 3);
		}

		private Label Remain(int index) => hud.SlotRow[index].Q<Label>(className: RunEquipmentHud.USS_REMAIN);
		private Label State(int index) => hud.SlotRow[index].Q<Label>(className: RunEquipmentHud.USS_STATE);
		private bool IsFlashing(int index) => hud.SlotRow[index].Q(className: RunEquipmentHud.USS_FLASH).ClassListContains(RunEquipmentHud.USS_FLASH_ON);

		private class FakeCooldown : IEquipmentCooldown
		{
			public CooldownState Cooldown { get; set; }
		}
	}
}
