namespace WitchMendokusai.DomainSDK.Homestead
{
	// 행동 하나의 판정. 실패는 사유와 걸린 대상 (물건이나 건물 id) 을 함께
	public readonly struct HomesteadResult
	{
		public readonly HomesteadFailure Failure;
		public readonly string Subject;

		private HomesteadResult(HomesteadFailure failure, string subject)
		{
			Failure = failure;
			Subject = subject;
		}

		public bool Ok => Failure == HomesteadFailure.None;

		public static readonly HomesteadResult Success = new HomesteadResult(HomesteadFailure.None, string.Empty);

		public static HomesteadResult Fail(HomesteadFailure failure, string subject = "") => new HomesteadResult(failure, subject);
	}

	// 창고에 들어온 것 하나. 처음 얻은 물건이면 IsNew
	public readonly struct HomesteadGain
	{
		public readonly string GoodsId;
		public readonly int Amount;
		public readonly bool IsNew;

		public HomesteadGain(string goodsId, int amount, bool isNew)
		{
			GoodsId = goodsId;
			Amount = amount;
			IsNew = isNew;
		}
	}
}
