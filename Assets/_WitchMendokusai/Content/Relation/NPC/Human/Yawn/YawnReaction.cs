using UnityEngine;

namespace WitchMendokusai
{
	public class YawnReaction : MonoBehaviour
	{
		private void OnEnable()
		{
			GameEventManager.Instance.RegisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
		}

		private void OnDisable()
		{
			GameEventManager.Instance.UnregisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
		}

		private void OnDungeonReturn()
		{
			UIManager.Instance.SpeechBubble.Show(transform, "...수고했어.");
		}

		[ContextMenu("Test/OnDungeonReturn")]
		private void TestOnDungeonReturn()
		{
			OnDungeonReturn();
		}
	}
}
