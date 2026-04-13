using UnityEngine;

namespace WitchMendokusai
{
	public class YawnReaction : MonoBehaviour
	{
		private void OnEnable()
		{
			GameEventManager.Instance.RegisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			GameEventManager.Instance.RegisterCallback(GameEventType.OnResearchComplete, OnResearchComplete);
		}

		private void OnDisable()
		{
			GameEventManager.Instance.UnregisterCallback(GameEventType.OnDungeonReturn, OnDungeonReturn);
			GameEventManager.Instance.UnregisterCallback(GameEventType.OnResearchComplete, OnResearchComplete);
		}

		private void OnDungeonReturn()
		{
			UIManager.Instance.SpeechBubble.Show(transform, "...수고했어.");
		}

		private void OnResearchComplete()
		{
			UIManager.Instance.SpeechBubble.Show(transform, "오, 뭔가 알아냈어?");
		}

		[ContextMenu("Test/OnDungeonReturn")]
		private void TestOnDungeonReturn() => OnDungeonReturn();

		[ContextMenu("Test/OnResearchComplete")]
		private void TestOnResearchComplete() => OnResearchComplete();
	}
}
