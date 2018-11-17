namespace AggregateConsistency.Infrastructure
{
	public class StreamVersion
	{
		public string Stream { get; }
		public long Version { get; }

		public StreamVersion(string stream, long version) {
			Stream = stream;
			Version = version;
		}
	}
}