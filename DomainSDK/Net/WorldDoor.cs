namespace WitchMendokusai.Net
{
	/// <summary>세계로 들어가는 문 — 실제로 어떻게 붙는지는 통신 층이 안다 (TASK-WM-217).</summary>
	public interface IWorldDoor
	{
		/// <summary>싱글, 내 안의 세계로 입장. 서버 접속 없음. 이미 그 세계면 그대로.</summary>
		void EnterSolo();

		/// <summary>멀티, 멀리 있는 세계로 입장. 이미 접속 중이면 그대로.</summary>
		void EnterShared();

		/// <summary>세계에서 나온다. 내 안의 세계는 저장하고, 멀리 있는 세계는 접속을 끊는다. 다음 입장은 새로 붙는다.</summary>
		void Leave();

		/// <summary>내 안의 세계를 지울 수 있나. 어느 세계에든 들어가 있거나 다른 프로세스가 그 세계를 쓰면 false.</summary>
		bool CanResetSolo { get; }

		/// <summary>내 안의 세계를 지워 다음 싱글 입장이 빈 세계에서 시작하게. 멀리 있는 세계는 그대로. CanResetSolo 가 false 면 던짐.</summary>
		void ResetSolo();

		/// <summary>지금 이어진 줄 — 아직 안 들어갔으면 null.</summary>
		IWorldLink Current { get; }
	}

	/// <summary>
	/// 로비가 통신 층을 <b>몰라도</b> 세계로 들어갈 수 있게 하는 자리 (TASK-WM-217).
	///
	/// ★ 왜 필요했나: 로비(WM.Domain)가 문(WM.Network)의 타입을 직접 부르자 컴파일이 깨졌다 —
	///   어셈블리 방향이 <b>Network → Domain</b> 단방향이기 때문이다. 방향을 뒤집는 대신
	///   저장소의 기존 규약(Bridge)을 쓴다: 판정 층에 구멍만 두고 통신 층이 자기를 꽂는다.
	///
	/// 문은 <b>스스로 선다</b>(WM.Network 의 부팅 훅) — 로비가 만들어 주지 않는다.
	/// </summary>
	public static class WorldDoor
	{
		private static IWorldDoor door;

		/// <summary>문이 준비됐나 — 아직이면 로비가 기다릴 이유는 없다(들어가기는 그냥 무시된다).</summary>
		public static bool IsReady => door != null;

		/// <summary>지금 이어진 줄.</summary>
		public static IWorldLink Current => door?.Current;

		/// <summary>통신 층이 부팅할 때 자기를 꽂는다.</summary>
		public static void Register(IWorldDoor instance) => door = instance;

		/// <summary>싱글 입장.</summary>
		public static void EnterSolo() => door?.EnterSolo();

		/// <summary>멀티 입장.</summary>
		public static void EnterShared() => door?.EnterShared();

		/// <summary>나가기 — 월드에서 로비로 돌아갈 때.</summary>
		public static void Leave() => door?.Leave();

		/// <summary>내 안의 세계를 지울 수 있나. 문이 안 섰으면 던짐.</summary>
		public static bool CanResetSolo => door.CanResetSolo;

		/// <summary>내 안의 세계를 지움. 문이 안 섰으면 못 지운 것이라 조용히 넘기지 않고 던짐.</summary>
		public static void ResetSolo() => door.ResetSolo();
	}
}
