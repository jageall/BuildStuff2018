namespace AggregateConsistency.Infrastructure
{
	public struct ExpectedVersion
	{
		public ExpectedVersion(long any, long exists, long doesNotExist) {
			Any = any;
			Exists = exists;
			DoesNotExist = doesNotExist;
		}

		public long Any { get; }
		public long Exists { get; }
		public long DoesNotExist { get; }
	}
}