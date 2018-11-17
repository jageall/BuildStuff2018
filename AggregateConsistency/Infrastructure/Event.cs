using System;
using Newtonsoft.Json;

namespace AggregateConsistency.Infrastructure
{
	public abstract class Event : IEvent
	{
        [JsonIgnore]
        public Guid Id { get; internal set; }

		public Event()
        {
		}

		[JsonIgnore]
		IMetadata IEvent.Metadata { get; set; }
	}
}