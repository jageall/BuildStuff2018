using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public interface IEventDeserializer
	{
		Task Deserialize(
			string scopeIdentity,
			Type scope,
			Dictionary<string, long> streamRevisions,
			Func<Task<SerializedEvent>> events,
			Action<Event> onEvent);
	}
}