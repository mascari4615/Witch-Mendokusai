using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace WitchMendokusai
{
	public class GameEventListener : MonoBehaviour
	{
		// public GameEvent Event;
		[field: SerializeField] public GameEventType EventType { get; private set; }

		[field: SerializeField] public UnityEvent Response { get; private set; }
		[field: SerializeField] public List<EffectInfo> Effects { get; private set; }

		private void OnEnable()
		{
			// Event.RegisterListener(this);
			GameEventManager.Instance.RegisterCallback(EventType, OnEventRaised);
		}

		private void OnDisable()
		{
			// Event.UnregisterListener(this);
			GameEventManager.Instance.UnregisterCallback(EventType, OnEventRaised);
		}

		public void OnEventRaised()
		{
			// Debug.Log($"{name} : OnEventRaised");
			Response.Invoke();
			Effect.ApplyEffects(Effects);
			// Debug.Log($"{name} : OnEventRaisedEnd");
		}
	}
}