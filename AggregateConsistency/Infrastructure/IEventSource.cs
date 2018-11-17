using System;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public interface IEventSource
	{
		Func<Task<SerializedEvent>> ReadEventsForward(string stream, long start, int batchSize, Action<string> onNotFound, bool resolveLinks = true);
		Func<Task<SerializedEvent>> ReadEventsBackwards(string stream, int batchSize, long maxToRead, Action<string> onNotFound, bool resolveLinks = true);
	}
}