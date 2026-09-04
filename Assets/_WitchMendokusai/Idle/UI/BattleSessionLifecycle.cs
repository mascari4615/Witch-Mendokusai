using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	/// <summary>전투 화면의 세션 생성, 저장과 종료 수명주기</summary>
	internal sealed class BattleSessionLifecycle
	{
		private readonly SessionPersistence persistence;

		public BattleSessionLifecycle(IdleTuning tuning, RuntimeSettingsSO settings, bool preview)
		{
			IsPreview = preview;

			if (preview)
			{
				Session = new IdleSession(tuning, settings.CreatePreviewState(tuning));
				Away = default;
				return;
			}

			persistence = new SessionPersistence(settings.SaveIntervalSeconds);
			Session = new IdleSession(tuning, persistence.LoadState());
			Away = persistence.CatchUp(Session);
		}

		public IdleSession Session { get; }

		public IdleAwayReport Away { get; }

		public bool IsPreview { get; }

		public void TickPersistence(float delta)
		{
			persistence?.Tick(delta, Session);
		}

		public void Save()
		{
			persistence?.Save(Session);
		}

		public void WipeAndSkipClose()
		{
			persistence.WipeAndSkipClose();
		}

		public void Close()
		{
			persistence?.Close(Session);
		}
	}
}
