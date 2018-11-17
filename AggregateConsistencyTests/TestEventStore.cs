using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AggregateConsistency.Infrastructure;
using Newtonsoft.Json;
using Xunit;

namespace AggregateConsistencyTests
{
    internal class TestEventStore : IEventStore
    {
        private readonly Dictionary<string, IReadOnlyList<SerializedEvent>> _streams;

        private readonly IEventSerializer _eventSerializer;
        private readonly IEventDeserializer _eventDeserializer;
        private readonly Dictionary<string, int> _streamPositions;
        
        public ExpectedVersion ExpectedVersion { get; }
        
        public TestEventStore(IKnownSerializers serializers)
        {
            _eventSerializer = serializers.Events.Serializer;
            _eventDeserializer = serializers.Events.Deserializer;
            _streams = new Dictionary<string, IReadOnlyList<SerializedEvent>>();
            _streamPositions = new Dictionary<string, int>();
            ExpectedVersion = new ExpectedVersion(-1, -2, -3);
        }
        
        public Task<long> Append(string stream, long expectedVersion, IReadOnlyList<SerializedEvent> events)
        {
            if (expectedVersion != ExpectedVersion.Any)
            {
                if (_streams.ContainsKey(stream) && expectedVersion == ExpectedVersion.DoesNotExist)
                {
                    throw new Exception("Stream exists");
                }

                if (!_streams.ContainsKey(stream) && expectedVersion == ExpectedVersion.Exists)
                {
                    throw new Exception("Stream does not exist");
                }

                if (expectedVersion != ExpectedVersion.Any && expectedVersion != ExpectedVersion.DoesNotExist &&
                    _streams[stream].Count - 1 != expectedVersion)
                {
                    throw new Exception("Expected version does not match");
                }
            }

            List<SerializedEvent> eventStream = new List<SerializedEvent>();
            
            int startAt = 0;

            if (_streams.TryGetValue(stream, out IReadOnlyList<SerializedEvent> existing))
            {
                eventStream.AddRange(existing);
                startAt = existing.Count;
            }
            eventStream.AddRange(events.Select((x, i) => new SerializedEvent(x.Id, x.Type, x.Data, x.Metadata, stream, i + startAt)));
            _streams[stream] = eventStream;
            return Task.FromResult(eventStream.Last().StreamRevision);
        }
        
        public Func<Task<SerializedEvent>> ReadEventsForward(string stream, long start, int batchSize, Action<string> notFound, bool resolveLinks = true)
        {
            if (_streams.TryGetValue(stream, out IReadOnlyList<SerializedEvent> events))

            {
                return new StreamIterator(events.Skip((int)start).GetEnumerator()).Next;

            }

            notFound(stream);

            return () => Task.FromResult(SerializedEvent.None);

        }
        
        public Func<Task<SerializedEvent>> ReadEventsBackwards(string stream, int batchSize, long maxToRead, Action<string> notFound, bool resolveLinks = true)
        {
            if (_streams.TryGetValue(stream, out IReadOnlyList<SerializedEvent> events))
            {
                IEnumerable<SerializedEvent> eventsToRead = events.Reverse();
                if (maxToRead > 0)
                {
                    eventsToRead = eventsToRead.Take((int)maxToRead);
                }
                return new StreamIterator(eventsToRead.GetEnumerator()).Next;
            }

            notFound(stream);
            return () => Task.FromResult(SerializedEvent.None);
        }

        private class StreamIterator
        {
            private readonly IEnumerator<SerializedEvent> _enumerator;
            public StreamIterator(IEnumerator<SerializedEvent> enumerator)
            {
                _enumerator = enumerator;
            }
            public Task<SerializedEvent> Next()
            {
                if (_enumerator.MoveNext())
                {
                    return Task.FromResult(_enumerator.Current);
                }

                return Task.FromResult(SerializedEvent.None);

            }
        }
        public IReadOnlyDictionary<string, IReadOnlyList<SerializedEvent>> Streams => _streams;
       
        public TestEventStore WithEvents(string scopeIdentity, string stream, params Event[] events)
        {
            return WithEvents(stream, events.Select(x => _eventSerializer.Serialize(scopeIdentity, x)).ToArray());
        }
        
        public void AssertEvents(string scopeIdentity, string stream, params Event[] expected)
        {
            _streamPositions.TryGetValue(stream, out int position);
            
            Assert.True(Streams.TryGetValue(stream, out IReadOnlyList<SerializedEvent> actual), $"Expected stream '{stream}' was not found");
            var doesntMatter = new Dictionary<string, long>()
            {
                [stream] = ExpectedVersion.Exists
            };
            _streamPositions.TryGetValue(stream, out var start);
            List<Event> actualEvents = new List<Event>();
            _eventDeserializer.Deserialize(scopeIdentity, typeof(object), doesntMatter,
                ReadEventsForward(stream, start, 1000, _ => { }, true), e => actualEvents.Add(e)).Wait();
            Assert.Equal(EventsToString(expected), 
                EventsToString(actualEvents));
        }
        
        private void MarkStreamPositions()
        {
            foreach (KeyValuePair<string, IReadOnlyList<SerializedEvent>> kv in _streams)
            {
                _streamPositions[kv.Key] = kv.Value.Count;
            }
        }

        private string EventsToString(IEnumerable<Event> events)
        {
            return string.Join(Environment.NewLine, events.Select(AsJson));
        }

        private static string AsJson(Event e)
        {
            return JsonConvert.SerializeObject(e);
        }
        private TestEventStore WithEvents(string stream, params SerializedEvent[] events)
        {
            Append(stream, ExpectedVersion.Any, events);
            MarkStreamPositions();
            return this;
        }
    }
}