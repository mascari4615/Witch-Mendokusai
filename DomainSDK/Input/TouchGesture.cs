using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 손가락 몸짓을 읽는 계산기 — 「몇 개가 어디에 닿아 있나」만 먹고 「끌기·오므리기·톡」을 낸다 (TASK-WM-200).
	///
	/// ★ 왜 Unity 입력 장치와 떨어져 있나: 「톡 친 것인가 끈 것인가」는 *판정*이지 장치가 아니다.
	///   장치에 붙여 두면 손가락 없는 컴퓨터에서 영영 검증할 수 없고, 그 판정이 바로 모바일 조작의
	///   전부다(잘못 재면 지도를 끌다가 건물이 서 버린다). 그래서 좌표만 먹는 순수 계산으로 둔다.
	///
	/// ★ 좌표계: 화면 픽셀, 좌하단 원점 — Unity 입력 계열과 같다.
	/// </summary>
	public sealed class TouchGesture
	{
		private readonly List<Vector2> current = new();
		private readonly List<Vector2> previous = new();
		private readonly List<int> currentIds = new();
		private readonly List<int> previousIds = new();

		private Vector2 pressStartPosition;
		private float pressSeconds;
		private float pressTravel;
		private bool wasPressed;
		private bool multiTouched;
		private float twistPending;
		private bool twistUnlocked;

		public TouchGestureTuning Tuning { get; set; } = TouchGestureTuning.Default;

		/// <summary> 지금 닿아 있는 손가락 수. </summary>
		public int TouchCount => current.Count;

		/// <summary> 첫 손가락 자리. 아무도 안 닿았으면 마지막으로 닿았던 자리를 유지한다(마우스 커서와 같은 성질). </summary>
		public Vector2 PrimaryPosition { get; private set; }

		/// <summary> 첫 손가락이 이번 프레임에 움직인 양. 손가락 수가 바뀐 프레임은 0 — 안 그러면 뗄 때 화면이 튄다. </summary>
		public Vector2 PrimaryDelta { get; private set; }

		/// <summary> 두 손가락 사이가 벌어진 양(+) / 좁혀진 양(-). 픽셀. </summary>
		public float PinchDelta { get; private set; }

		/// <summary> 두 손가락 가운데점이 움직인 양 — 두 손가락 끌기. </summary>
		public Vector2 TwoFingerPanDelta { get; private set; }

		/// <summary> 두 손가락이 돌아간 양(도). 시계 반대 방향이 +. </summary>
		public float TwistDelta { get; private set; }

		/// <summary> 이번 프레임에 손가락이 처음 닿았다. </summary>
		public bool PressedThisFrame { get; private set; }

		/// <summary> 이번 프레임에 마지막 손가락이 떨어졌다. </summary>
		public bool ReleasedThisFrame { get; private set; }

		/// <summary> 이번 프레임에 「톡」이 끝났다 — 짧게 닿았다 뗐고 거의 안 움직였다. </summary>
		public bool TappedThisFrame { get; private set; }

		/// <summary> 톡이 일어난 자리 (TappedThisFrame 인 프레임에만 뜻이 있다). </summary>
		public Vector2 TapPosition { get; private set; }

		/// <summary> 한 손가락으로 끌고 있다 — 문턱을 넘은 뒤로는 뗄 때까지 계속 참이다. </summary>
		public bool IsDragging { get; private set; }

		/// <summary> 손가락이 하나라도 닿아 있다. </summary>
		public bool IsPressed => current.Count > 0;

		/// <summary>
		/// 한 프레임 분을 먹인다. positions = 지금 닿아 있는 손가락들의 화면 좌표(순서는 안정적이어야 한다).
		/// </summary>
		public void Update(IReadOnlyList<Vector2> positions, float deltaSeconds)
		{
			Update(positions, null, deltaSeconds);
		}

		/// <summary>
		/// 손가락 이름표(ids)까지 같이 먹인다.
		///
		/// ★ 왜 이름표가 필요한가: 수만 세면 「한 손가락이 떨어지고 다른 손가락이 같은 프레임에
		///   닿은 것」과 「그 손가락들이 그대로 움직인 것」을 구별할 수 없다. 둘 다 둘이다. 그러면
		///   전혀 다른 두 자리 사이의 거리가 「움직인 양」으로 잡혀 화면이 순간이동한다.
		/// ★ 이름표를 안 주면 예전처럼 수만 본다 — 마우스처럼 이름표가 없는 장치도 있기 때문이다.
		/// </summary>
		public void Update(IReadOnlyList<Vector2> positions, IReadOnlyList<int> ids, float deltaSeconds)
		{
			previous.Clear();
			previous.AddRange(current);
			current.Clear();
			previousIds.Clear();
			previousIds.AddRange(currentIds);
			currentIds.Clear();
			if (positions != null)
			{
				for (int i = 0; i < positions.Count; i++)
					current.Add(positions[i]);
			}
			if (ids != null)
			{
				for (int i = 0; i < ids.Count; i++)
					currentIds.Add(ids[i]);
			}

			bool countChanged = current.Count != previous.Count || IdsChanged();
			bool isPressed = current.Count > 0;

			PressedThisFrame = isPressed && wasPressed == false;
			ReleasedThisFrame = isPressed == false && wasPressed;
			TappedThisFrame = false;
			PrimaryDelta = Vector2.zero;
			PinchDelta = 0f;
			TwoFingerPanDelta = Vector2.zero;
			TwistDelta = 0f;

			if (isPressed)
			{
				PrimaryPosition = current[0];

				// 손가락 수가 바뀐 프레임의 델타는 「움직임」이 아니라 「다른 손가락으로 갈아탄 것」이다.
				if (countChanged == false && previous.Count > 0)
					PrimaryDelta = current[0] - previous[0];

				// ★ 두 손가락이 한 번이라도 얹혔으면 이 누름은 「톡」이 될 수 없다. 오므리기는 첫
				//   손가락이 거의 안 움직이고 순식간에 끝날 수 있어서, 시간·흔들림만 재면 확대하고
				//   손을 뗀 자리에 건물이 서 버린다 — 이 파일이 막으려던 바로 그 사고다.
				if (current.Count >= 2)
					multiTouched = true;

				if (PressedThisFrame)
				{
					pressStartPosition = current[0];
					pressSeconds = 0f;
					pressTravel = 0f;
					multiTouched = current.Count >= 2;
					IsDragging = false;
				}
				else
				{
					pressSeconds += deltaSeconds;
					pressTravel += PrimaryDelta.magnitude;
					// 문턱을 한 번 넘으면 끌기다 — 손가락이 잠깐 멈춰도 끌기가 풀리면 화면이 덜컹거린다.
					if (IsDragging == false && pressTravel > Tuning.DragSlopPixels)
						IsDragging = true;
				}

				if (current.Count >= 2 && previous.Count >= 2 && countChanged == false)
				{
					PinchDelta = Vector2.Distance(current[0], current[1]) - Vector2.Distance(previous[0], previous[1]);
					TwoFingerPanDelta = Midpoint(current) - Midpoint(previous);
					TwistDelta = Twist(Mathf.DeltaAngle(AngleOf(previous), AngleOf(current)));
				}
			}
			else
			{
				if (ReleasedThisFrame)
				{
					// 톡 = 짧게 + 거의 안 움직임. 둘 중 하나라도 어기면 끌기였다.
					if (multiTouched == false
						&& pressSeconds <= Tuning.TapMaxSeconds
						&& pressTravel <= Tuning.TapMaxTravelPixels
						&& Vector2.Distance(PrimaryPosition, pressStartPosition) <= Tuning.TapMaxTravelPixels)
					{
						TappedThisFrame = true;
						TapPosition = PrimaryPosition;
					}
				}
				IsDragging = false;
			}

			// 두 손가락이 아니게 된 순간 「돌릴 마음」 판정을 처음으로 되돌린다 — 다음에 다시
			// 두 손가락을 얹었을 때 지난번 문턱이 그대로 열려 있으면 얹자마자 화면이 돈다.
			if (current.Count < 2)
			{
				twistPending = 0f;
				twistUnlocked = false;
			}

			wasPressed = isPressed;
		}

		/// <summary> 장치가 사라졌을 때 등 — 눌린 상태가 남아 굳는 것을 막는다. </summary>
		public void Reset()
		{
			current.Clear();
			previous.Clear();
			currentIds.Clear();
			previousIds.Clear();
			wasPressed = false;
			IsDragging = false;
			PressedThisFrame = false;
			ReleasedThisFrame = false;
			TappedThisFrame = false;
			PrimaryDelta = Vector2.zero;
			PinchDelta = 0f;
			TwoFingerPanDelta = Vector2.zero;
			TwistDelta = 0f;
			pressSeconds = 0f;
			pressTravel = 0f;
			multiTouched = false;
			twistPending = 0f;
			twistUnlocked = false;
		}

		/// <summary>
		/// 「돌리려던 것인가, 오므리다 딸려 돈 것인가」를 가른다.
		///
		/// ★ 두 손가락으로 확대하거나 밀 때 손은 *반드시* 몇 도씩 딸려 돈다. 그 값을 그대로
		///   시점 회전에 먹이면, 확대만 하려 해도 화면이 같이 빙 돈다 — 손가락이 닿는 순간부터
		///   모든 몸짓이 서로를 오염시킨다.
		/// ★ 그래서 문턱까지는 *모아만 두고* 내보내지 않는다. 살짝 갔다 돌아온 흔들림은 부호가
		///   달라 서로 지워지고, 진짜로 돌리려 한 손만 문턱을 넘는다. 한 번 넘으면 손가락을 뗄
		///   때까지 그대로 통과시킨다 — 중간에 다시 잠그면 돌리는 도중 화면이 덜컹거린다.
		/// ★ 문턱을 넘은 첫 프레임엔 *모아 둔 값 전부*를 내보낸다. 안 그러면 그 각도만큼
		///   화면이 안 따라와서, 손가락과 지도가 어긋난 채로 남는다.
		/// </summary>
		private float Twist(float rawDegrees)
		{
			if (twistUnlocked)
				return rawDegrees;

			twistPending += rawDegrees;
			if (Mathf.Abs(twistPending) < Tuning.TwistDeadZoneDegrees)
				return 0f;

			twistUnlocked = true;
			float released = twistPending;
			twistPending = 0f;
			return released;
		}

		/// <summary> 지난 프레임과 *다른 손가락들*인가. 이름표를 안 받는 장치는 언제나 거짓. </summary>
		private bool IdsChanged()
		{
			if (currentIds.Count == 0 || currentIds.Count != previousIds.Count)
				return false;

			for (int i = 0; i < currentIds.Count; i++)
			{
				if (currentIds[i] != previousIds[i])
					return true;
			}
			return false;
		}

		private static Vector2 Midpoint(List<Vector2> points) => (points[0] + points[1]) * 0.5f;

		private static float AngleOf(List<Vector2> points)
		{
			Vector2 span = points[1] - points[0];
			return Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
		}
	}

	/// <summary>
	/// 몸짓 판정 문턱값 — 손가락은 마우스보다 뭉툭해서 「안 움직였다」의 기준이 다르다.
	/// 화면 밀도가 다른 기기에서 다시 재야 하므로 값으로 꺼내 둔다 (수치 노출 룰).
	/// </summary>
	public struct TouchGestureTuning
	{
		/// <summary> 이 시간 안에 떼야 「톡」으로 친다 (초). </summary>
		public float TapMaxSeconds;

		/// <summary> 이만큼까지 흔들려도 「톡」으로 친다 (픽셀). </summary>
		public float TapMaxTravelPixels;

		/// <summary> 이만큼 넘게 움직이면 「끌기」로 굳는다 (픽셀). </summary>
		public float DragSlopPixels;

		/// <summary> 두 손가락이 이만큼 돌아가기 전엔 「돌린 것」으로 안 친다 (도). 오므리다 딸려 도는 각도 거름망. </summary>
		public float TwistDeadZoneDegrees;

		public static TouchGestureTuning Default => new TouchGestureTuning
		{
			TapMaxSeconds = 0.35f,
			TapMaxTravelPixels = 24f,
			DragSlopPixels = 12f,
			TwistDeadZoneDegrees = 8f,
		};
	}
}
