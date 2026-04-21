using System.Collections;
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
			StartCoroutine(ReturnReactionRoutine());
		}

		private IEnumerator ReturnReactionRoutine()
		{
			YawnIdleBehavior idle = GetComponent<YawnIdleBehavior>();
			YawnIdleState state = idle != null ? idle.CurrentState : YawnIdleState.Zoning;

			if (state == YonIdleState.WindowGazing)
				yield return new WaitForSeconds(2f);

			UIManager.Instance.SpeechBubble.Show(transform, GetReturnReaction(state));
		}

		private static string GetReturnReaction(YawnIdleState state) => state switch
		{
			YawnIdleState.Reading => "아, 왔구나.",
			YawnIdleState.Searching => "어 잠깐만, 어디 뒀더라...",
			YawnIdleState.WindowGazing => "...왔어?",
			_ => "...수고했어."
		};

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
