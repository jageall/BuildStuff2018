using System;
using Newtonsoft.Json;

namespace AggregateConsistency.Infrastructure
{
	public abstract class Event : IEvent
	{
		public Guid Id { get; }

		public Event(Guid id) {
			Id = id;
		}

		[JsonIgnore]
		IMetadata IEvent.Metadata { get; set; }
	}
}