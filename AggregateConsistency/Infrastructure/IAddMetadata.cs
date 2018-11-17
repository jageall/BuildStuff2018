using System.Collections.Generic;

namespace AggregateConsistency.Infrastructure
{
	public interface IAddMetadata
	{
		void Enrich(IReadOnlyList<Event> events);
	}
}