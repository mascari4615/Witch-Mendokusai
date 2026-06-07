namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 상품 1개 제조에 필요한 재료 종류·수량. <see cref="WorkshopProduct"/> 의 레시피 원소.
	/// 순수 readonly struct — 비교·전송 가벼움.
	/// </summary>
	public readonly struct MaterialCost
	{
		public readonly MaterialId Material;
		public readonly int Amount;

		public MaterialCost(MaterialId material, int amount)
		{
			Material = material;
			Amount = amount;
		}
	}
}
