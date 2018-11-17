namespace AggregateConsistency.Infrastructure
{
	public interface IEventSerializer
	{
		SerializedEvent Serialize(string scopeIdentity, Event @event);
	}
}