using System;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 목장 동물이 서는 자리. 문 앞 마당에 칸을 나눠 한 마리씩
	// 가운데 길은 비워 둠: 문 앞에 서면 목장 자리가 가장 가깝고, 동물 곁에 서면 그 동물이 가장 가까움
	// 좌표는 문 기준. Side 는 문을 보고 옆, Ahead 는 문에서 앞으로 (VillageStage.DoorPoint 와 같은 뜻)
	[Serializable]
	public sealed class RanchYard
	{
		// 이웃 칸 사이 (줄과 줄, 같은 쪽 칸과 칸)
		public float Spacing = 1.5f;
		// 문에서 첫 줄까지
		public float FirstAhead = 1f;
		// 길 가운데에서 가장 안쪽 칸까지. 두 배가 길 양쪽 칸 사이
		public float InnerSide = 0.95f;
		// 한 줄 칸 수. 길 양쪽에 반씩
		public int Columns = 4;
		// 제자리에서 거니는 반지름. 칸 사이의 절반보다 작아야 이웃과 안 겹침
		public float Wander = 0.35f;

		// index 번째 동물의 제자리. 한 줄을 안쪽 칸부터 왼쪽, 오른쪽 번갈아 채우고 다음 줄로
		public (float Side, float Ahead) Home(int index)
		{
			int perSide = Math.Max(1, Columns / 2);
			int perRow = perSide * 2;
			int row = index / perRow;
			int inRow = index % perRow;
			int ring = inRow / 2;
			float sign = inRow % 2 == 0 ? -1f : 1f;
			return (sign * (InnerSide + ring * Spacing), FirstAhead + row * Spacing);
		}
	}
}
