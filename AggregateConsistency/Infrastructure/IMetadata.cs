namespace AggregateConsistency.Infrastructure
{
	public interface IMetadata : IReadOnlyMetadata
	{
		void Write<T>(string name, T value);
	}
}