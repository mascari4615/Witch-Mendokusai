namespace WitchMendokusai
{
	public struct LineData
	{
		public int unitID;
		public string line;
		public string additionalData;

		public LineData(ref string[] columns)
		{
			unitID = int.Parse(columns[1]);
			line = columns[2];
			additionalData = columns[3].TrimEnd('\r', '\n', ' ');
		}
	}
}