namespace AggregateConsistency.Infrastructure
{
	public interface IEventSerializer
	{
		SerializedEvent Serialize(string scope, Event @event);
	}
}