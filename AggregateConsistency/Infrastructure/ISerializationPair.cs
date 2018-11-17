namespace AggregateConsistency.Infrastructure
{
	public interface ISerializationPair<TSerializer, TDeserializer>
	{
		TSerializer Serializer { get; }
		TDeserializer Deserializer { get; }
	}
}