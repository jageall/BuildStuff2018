using System;

namespace AggregateConsistency.Infrastructure
{
	public interface ISnapshotDeserializer
	{
		Snapshot Deserialize(Type type, SerializedEvent value);
	}
}