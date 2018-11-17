namespace AggregateConsistency.Infrastructure
{
	public interface ISnapshotSerializer
	{
		SerializedEvent Serialize(object snapshot, long revision);
	}
}