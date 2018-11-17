using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public interface IEventStore : IEventSource
	{
		Task<long> Append(string stream, long expectedVersion, IReadOnlyList<SerializedEvent> events);

		ExpectedVersion ExpectedVersion { get; }
	}
}