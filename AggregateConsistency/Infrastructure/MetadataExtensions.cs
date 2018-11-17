using System.Diagnostics;

namespace AggregateConsistency.Infrastructure
{
	public static class MetadataExtensions
	{
		public static void AddMetadataValue<T>(this IEvent @event, string name, T value) {
			if(@event.Metadata == null) {
				SerializationRegistry.AddMetadataToEvent(@event);
			}
			Debug.Assert(@event.Metadata != null, "@event.Metadata != null");
			@event.Metadata.Write(name, value);
		}

		public static bool TryReadValue<T>(this IEvent @event, string name, out T value) {
			if(@event.Metadata != null)
				return @event.Metadata.TryRead<T>(name, out value);
			value = default(T);
			return false;
		}
	}
}