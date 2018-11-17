namespace AggregateConsistency.Infrastructure
{
	public interface IReadOnlyMetadata
	{
		long StreamRevision { get; }
		T ReadValueOrDefault<T>(string name, T @default = default(T));
		T Read<T>(string name);
		bool TryRead<T>(string name, out T value);
	}
}