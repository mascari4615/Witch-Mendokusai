using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// 키 바꾸기의 순수 규칙 (예약 키, 충돌 찾기, 맞바꿈). 입력 액션과 PlayerPrefs 는 건드리지 않음
	public class KeybindRegistryRebindTest
	{
		[TearDown]
		public void ClearOverrides() => KeybindRegistry.ClearOverrides();

		[Test]
		public void Reserved_key_is_rejected()
			=> Assert.AreEqual(KeybindRegistry.RebindCheck.Reserved, KeybindRegistry.Check(InputEventType.Status, "<Keyboard>/escape", out _));

		[Test]
		public void Movement_keys_are_reserved()
			=> Assert.IsTrue(KeybindRegistry.IsReserved("<Keyboard>/w"));

		[Test]
		public void Key_used_by_another_action_is_a_conflict_with_that_owner()
		{
			Assert.AreEqual(KeybindRegistry.RebindCheck.Conflict, KeybindRegistry.Check(InputEventType.Status, "<Keyboard>/i", out InputEventType owner));
			Assert.AreEqual(InputEventType.Inventory, owner);
		}

		[Test]
		public void Own_key_is_same()
			=> Assert.AreEqual(KeybindRegistry.RebindCheck.Same, KeybindRegistry.Check(InputEventType.Status, "<Keyboard>/v", out _));

		[Test]
		public void Unused_key_is_ok()
			=> Assert.AreEqual(KeybindRegistry.RebindCheck.Ok, KeybindRegistry.Check(InputEventType.Status, "<Keyboard>/u", out _));

		[Test]
		public void Swap_exchanges_paths_and_reset_restores_defaults()
		{
			KeybindRegistry.SwapPaths(InputEventType.Status, InputEventType.Inventory);
			Assert.AreEqual("<Keyboard>/i", KeybindRegistry.GetCurrentPath(InputEventType.Status));
			Assert.AreEqual("<Keyboard>/v", KeybindRegistry.GetCurrentPath(InputEventType.Inventory));
			Assert.IsTrue(KeybindRegistry.HasOverride(InputEventType.Status));

			KeybindRegistry.ClearOverrides();
			Assert.AreEqual("<Keyboard>/v", KeybindRegistry.GetCurrentPath(InputEventType.Status));
			Assert.IsFalse(KeybindRegistry.HasOverride(InputEventType.Status));
		}

		[Test]
		public void Setting_the_default_path_drops_the_override()
		{
			KeybindRegistry.SetPath(InputEventType.Status, "<Keyboard>/u");
			Assert.IsTrue(KeybindRegistry.HasOverride(InputEventType.Status));
			KeybindRegistry.SetPath(InputEventType.Status, "<Keyboard>/v");
			Assert.IsFalse(KeybindRegistry.HasOverride(InputEventType.Status));
		}

		[Test]
		public void Swap_does_not_create_a_conflict()
		{
			bool before = KeybindRegistry.HasActiveConflict();
			KeybindRegistry.SwapPaths(InputEventType.Status, InputEventType.Inventory);
			Assert.AreEqual(before, KeybindRegistry.HasActiveConflict());
		}

		[Test]
		public void Rebindable_means_keyboard_default_and_not_fixed()
		{
			Assert.IsTrue(KeybindRegistry.IsRebindable(InputEventType.Status));
			Assert.IsFalse(KeybindRegistry.IsRebindable(InputEventType.KeybindHelpToggle));
			Assert.IsFalse(KeybindRegistry.IsRebindable(InputEventType.Click0));
		}

		[Test]
		public void Layout_specific_path_is_normalized_to_default_form()
		{
			Assert.AreEqual("<Keyboard>/w", KeybindRegistry.NormalizePath("<Keyboard>/#(W)"));
			Assert.AreEqual("<Keyboard>/j", KeybindRegistry.NormalizePath("<Keyboard>/j"));
		}

		[Test]
		public void Key_names_are_readable()
		{
			Assert.AreEqual("Left Shift", KeybindFormat.FormatPath("<Keyboard>/leftShift"));
			Assert.AreEqual("J", KeybindFormat.FormatPath("<Keyboard>/j"));
			Assert.AreEqual(KeybindFormat.NONE_TEXT, KeybindFormat.FormatPath(null));
		}
	}
}
