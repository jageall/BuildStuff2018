namespace AggregateConsistency.Infrastructure
{
	public interface IEvent
	{
		IMetadata Metadata { get; set; }
	}
}