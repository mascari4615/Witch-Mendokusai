using System;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 목장 동물이 서는 자리. 문 줄 양옆에 칸을 나눠 한 마리씩
	// 가운데 길은 비워 둠: 문으로 가는 길 위에서는 목장 자리가 가장 가깝고, 동물 곁에 서면 그 동물이 가장 가까움
	// 2026-11-27 문 1m 앞 줄 (옆 0.95m, 거님 0.35m) 은 길에서 1.1m 떨어진 Z 가 닭을 잡았음
	// 좌표는 문 기준. Side 는 문을 보고 옆, Ahead 는 문에서 앞으로 (VillageStage.DoorPoint 와 같은 뜻)
	[Serializable]
	public sealed class RanchYard
	{
		// 이웃 칸 사이 (같은 쪽 칸과 칸, 줄과 줄)
		public float Spacing = 1.4f;
		// 문 줄에서 첫 줄까지. 0 이면 문 줄 위. 앞으로 나오면 길 끝에서 동물이 문보다 가까워짐
		public float FirstAhead = 0f;
		// 길 가운데에서 가장 안쪽 칸까지. 두 배가 길 양쪽 칸 사이
		public float InnerSide = 1.3f;
		// 한 줄 칸 수. 길 양쪽에 반씩. 여섯이면 목장 3단 여섯 마리가 한 줄
		public int Columns = 6;
		// 제자리에서 거니는 반지름. 칸 사이의 절반보다 작아야 이웃과 안 겹침
		public float Wander = 0.25f;

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

		// 동물 줄 바깥 왼쪽 끝. 목장에서 일하는 인형이 동물 칸과 문 길을 피해 섬
		public (float Side, float Ahead) Beyond() => (-(InnerSide + Math.Max(1, Columns / 2) * Spacing), FirstAhead);
	}
}
