using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AggregateConsistency.Infrastructure
{
	public abstract class Repository
	{
		protected readonly IEventStore _store;
		protected readonly IEventSerializer _eventSerializer;
		protected readonly IEventDeserializer _eventDeserializer;

		protected Repository(IEventStore store, IKnownSerializers serializers) {
			_store = store;
			_eventSerializer = serializers.Events.Serializer;
			_eventDeserializer = serializers.Events.Deserializer;
		}

		protected long Any => _store.ExpectedVersion.Any;
		protected long Exists => _store.ExpectedVersion.Exists;
		protected long DoesNotExist => _store.ExpectedVersion.DoesNotExist;
	}
}
