using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	public sealed class SessionPersistence
	{
		private readonly float saveIntervalSeconds;
		private float elapsed;
		private bool skipNextClose;

		public SessionPersistence(float saveIntervalSeconds)
		{
			this.saveIntervalSeconds = saveIntervalSeconds;
		}

		public IdleState LoadState()
		{
			IdleState state = new IdleState();
			IdleSaveData? saved = SaveStore.Load();
			if (saved.HasValue)
			{
				state.Load(saved.Value);
			}
			return state;
		}

		public IdleAwayReport CatchUp(IdleSession session)
		{
			session.CatchUp(SaveStore.NowUnixSeconds(), out IdleAwayReport away);
			return away;
		}

		public void Tick(float delta, IdleSession session)
		{
			elapsed += delta;
			if (elapsed >= saveIntervalSeconds)
			{
				Save(session);
			}
		}

		public void Save(IdleSession session)
		{
			if (session == null)
			{
				return;
			}

			elapsed = 0f;
			session.MarkSeen(SaveStore.NowUnixSeconds());
			SaveStore.Save(session.State.Save());
		}

		public void WipeAndSkipClose()
		{
			skipNextClose = true;
			SaveStore.Wipe();
		}

		public void Close(IdleSession session)
		{
			if (skipNextClose)
			{
				skipNextClose = false;
				return;
			}

			Save(session);
		}
	}
}
