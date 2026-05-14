using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	public class GameEventListener : MonoBehaviour
	{
		// public GameEvent Event;
		[field: SerializeField] public GameEventType EventType { get; private set; }

		[field: SerializeField] public UnityEvent Response { get; private set; }
		[field: SerializeField] public List<EffectInfo> Effects { get; private set; }

		private GameEventManager gameEventManager;

		[Inject]
		public void Construct(GameEventManager gameEventManager)
		{
			this.gameEventManager = gameEventManager;
		}

		private void Awake()
		{
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
		}

		private void OnEnable()
		{
			// Event.RegisterListener(this);
			gameEventManager.RegisterCallback(EventType, OnEventRaised);
		}

		private void OnDisable()
		{
			// Event.UnregisterListener(this);
			if (GameEventManager.TryGetExistingInstance(out GameEventManager manager))
				manager.UnregisterCallback(EventType, OnEventRaised);
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