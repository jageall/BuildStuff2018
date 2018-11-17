namespace AggregateConsistency.Infrastructure
{
	public interface IKnownSerializers
	{
		ISerializationPair<IEventSerializer, IEventDeserializer> Events { get; }
		ISerializationPair<ISnapshotSerializer, ISnapshotDeserializer> Snapshots { get; }
	}
}