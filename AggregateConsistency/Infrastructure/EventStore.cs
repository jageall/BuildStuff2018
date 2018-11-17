using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EV = EventStore.ClientAPI.ExpectedVersion;
namespace AggregateConsistency.Infrastructure
{
	public class EventStore : IEventStore
    {
        readonly IEventStoreConnection _connection;

        public EventStore(IEventStoreConnection connection) {
            _connection = connection;
            ExpectedVersion = new ExpectedVersion(EV.Any, EV.StreamExists, EV.NoStream);
        }

        /// <summary>
        /// Controls reading through event slices until the end of the stream is hit.
        /// </summary>
        class EventReader
        {
            readonly Func<long, Task<StreamEventsSlice>> _readSlice;
            readonly Action _onNotFound;
            long _start;
            readonly long _maxToRead;
            long _currentPostion;
            StreamEventsSlice _slice;

            public EventReader(
                Func<long, Task<StreamEventsSlice>> readSlice,
                Action onNotFound,
                long start,
                long maxToRead) {
                _readSlice = readSlice;
                _onNotFound = onNotFound;
                _start = start;
                _maxToRead = maxToRead;
            }

            public async Task<SerializedEvent> Next() {

                if(_slice != null && _currentPostion >= _slice.Events.Length && _slice.NextEventNumber < 0)
                    return SerializedEvent.None;

                if(_slice == null || _currentPostion >= _slice.Events.Length) {

                    _currentPostion = 0;
                    if(_slice != null) _start = _slice.NextEventNumber;
                    _slice = await _readSlice(_start);
                    if(_slice.Status != SliceReadStatus.Success)
                        _onNotFound();
                }
                if(_slice.Events.Length == 0 && _slice.IsEndOfStream) return SerializedEvent.None;
                var resolvedEvent = _slice.Events[_currentPostion];
                var e = resolvedEvent.Event;
                var result = new SerializedEvent(e.EventId, e.EventType, e.Data, e.Metadata, e.EventStreamId, resolvedEvent.OriginalEventNumber);
                _currentPostion++;
                return result;
            }
        }

        public Func<Task<SerializedEvent>> ReadEventsForward(string stream, long start, int batchSize, Action<string> onNotFound, bool resolveLinks = true) {
            return new EventReader(
                position => _connection.ReadStreamEventsForwardAsync(stream, position, batchSize, resolveLinks),
                () => onNotFound(stream),
                start,
                long.MaxValue).Next;
        }

        public Func<Task<SerializedEvent>> ReadEventsBackwards(string stream, int batchSize, long maxToRead, Action<string> onNotFound, bool resolveLinks = true) {
            return new EventReader(
                position => _connection.ReadStreamEventsBackwardAsync(stream, position, batchSize, resolveLinks),
                () => onNotFound(stream),
                StreamPosition.End,
                maxToRead).Next;
        }

        public async Task<long> Append(string stream, long expectedVersion,
            IReadOnlyList<SerializedEvent> events) {

            var result = await _connection.AppendToStreamAsync(
                stream,
                expectedVersion,
                events.Select(x => new EventData(x.Id, x.Type, true, x.Data, x.Metadata)));

            return result.NextExpectedVersion;
        }

        public ExpectedVersion ExpectedVersion { get; }
    }
}
