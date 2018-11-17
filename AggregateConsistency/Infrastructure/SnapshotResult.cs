namespace AggregateConsistency.Infrastructure
{
	public struct SnapshotResult
	{
		public SnapshotResult(long streamRevision, long revision) {
			StreamRevision = streamRevision;
			Revision = revision;
		}

		public long StreamRevision { get; }

		public long Revision { get; }
	}
}