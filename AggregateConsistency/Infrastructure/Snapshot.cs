namespace AggregateConsistency.Infrastructure
{
	public class Snapshot
	{
		public Snapshot(long streamRevision, long snapshotRevision, object value) {
			StreamRevision = streamRevision;
			SnapshotRevision = snapshotRevision;
			Value = value;
		}

		public object Value { get; }
		public long StreamRevision { get; }
		public long SnapshotRevision { get; }
	}
}