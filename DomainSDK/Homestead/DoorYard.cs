using System;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 건물 문 둘레에 서는 사람 (주민, 일하는 인형) 의 자리
	// 문으로 가는 길 위에서는 문이 가장 가까워야 Z 가 문을 잡음 (2026-11-27 시련소 사람이 문 앞을 막아 Z 가 그 사람을 잡던 것)
	// 그래서 사람은 문 줄 (Ahead 0) 옆에 서고 조금만 거님. 좌표 뜻은 RanchYard 와 같음
	[Serializable]
	public sealed class DoorYard
	{
		// 문 가운데에서 사람 제자리까지 옆으로
		public float PersonSide = 1.6f;
		// 사람이 제자리에서 거니는 반지름
		public float PersonWander = 0.3f;
		// 회관 앞에서 집을 기다리는 줄의 사이
		public float QueueGap = 0.8f;

		// 주민은 문 오른쪽
		public (float Side, float Ahead) Resident() => (PersonSide, 0f);

		// 일하는 인형은 문 왼쪽. 주민과 안 겹침
		public (float Side, float Ahead) Worker() => (-PersonSide, 0f);

		// 집이 아직인 주민은 회관 문 왼쪽으로 줄을 섬
		public (float Side, float Ahead) Queue(int index) => (-(PersonSide + index * QueueGap), 0f);
	}
}
