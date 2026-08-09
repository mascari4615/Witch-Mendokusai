namespace WitchMendokusai.Net
{
	/// <summary>세계로 들어가는 문 — 실제로 어떻게 붙는지는 통신 층이 안다 (TASK-WM-217).</summary>
	public interface IWorldDoor
	{
		/// <summary>세계로 들어간다. 이미 들어와 있으면 아무 일도 안 한다.</summary>
		void Enter();

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

		/// <summary>세계로 들어간다.</summary>
		public static void Enter() => door?.Enter();
	}
}
