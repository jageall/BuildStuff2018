using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AggregateConsistency.Infrastructure;

namespace AggregateConsistency
{
    public class UserRepository : Repository
    {
        private static readonly IReadOnlyDictionary<long, IReadOnlyList<Event>> NoLoginEvents =
            new Dictionary<long, IReadOnlyList<Event>>();

        public UserRepository(IEventStore store,
            IKnownSerializers serializers) : base(store, serializers)
        {
        }

        public async Task<LoadResult<User>> Load(string identifier, bool includeLoginEvents = false)
        {
            long start = 0;
            var user = (IEventSourcedState)Activator.CreateInstance(typeof(User), true);
            var userStream = UserStream(identifier);
            var loginStream = LoginStream(identifier);
            var expectedVersions = new Dictionary<string, long>()
            {
                [userStream] = DoesNotExist,
                [loginStream] = DoesNotExist
            };

            var loginEvents = includeLoginEvents ? await ReadLoginEvents(identifier, loginStream, expectedVersions) : NoLoginEvents;


            long lastRevision = 0;
            await _eventDeserializer.Deserialize(
                identifier,
                typeof(User),
                expectedVersions,
                _store.ReadEventsForward(userStream, start, 100, _ =>
                {
                }), e =>
                {
                    user.Apply(e);
                    var em = (IEvent)e;
                    if (em.Metadata.StreamRevision != lastRevision)
                    {
                        ApplyLoginEventsForThisRevision(loginEvents, lastRevision, user);
                    }

                    lastRevision = em.Metadata.StreamRevision;
                });
            ApplyLoginEventsForThisRevision(loginEvents, lastRevision, user);
            if (loginEvents.Keys.Any(x => x > lastRevision))
            {
                throw new InvalidOperationException("login events have revisions greater than stream revision");
            }

            return new LoadResult<User>((User)user, expectedVersions.Select(x => new StreamVersion(x.Key, x.Value)).ToArray());

        }

        private static void ApplyLoginEventsForThisRevision(IReadOnlyDictionary<long, IReadOnlyList<Event>> loginEvents, long lastRevision, IEventSourcedState user)
        {
            if (loginEvents.TryGetValue(lastRevision, out var toApply))
            {
                foreach (var @event in toApply)
                {
                    user.Apply(@event);
                }
            }
        }

        private static string LoginStream(string identifier)
        {
            return $"userLogin-{identifier}";
        }

        private static string UserStream(string identifier)
        {
            return $"user-{identifier}";
        }

        private async Task<IReadOnlyDictionary<long, IReadOnlyList<Event>>> ReadLoginEvents(string identifier, string stream, Dictionary<string, long> expectedVersions)
        {

            var result = new Dictionary<long, List<Event>>();
            await _eventDeserializer.Deserialize(
                identifier,
                typeof(User), expectedVersions, _store.ReadEventsBackwards(stream, 5, 5, _ =>
                {
                }), e =>
                {
                    var em = (IEvent)e;
                    var userVersion = em.Metadata.Read<long>("userVersion");
                    if (!result.TryGetValue(userVersion, out var events))
                    {
                        events = new List<Event>();
                        result.Add(userVersion, events);
                    }
                    //Reverse event order
                    events.Insert(0, e);
                });
            return result.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Event>)kv.Value);
        }

        public Task<IReadOnlyList<StreamVersion>> SaveNew(string identifier, IReadOnlyList<Event> events)
        {
            return Save(identifier, events,
                new List<StreamVersion> {
                    new StreamVersion(UserStream(identifier), DoesNotExist),
                    new StreamVersion(LoginStream(identifier), DoesNotExist)
                });
        }

        public async Task<IReadOnlyList<StreamVersion>> Save(string identifier, IReadOnlyList<Event> events, IReadOnlyList<StreamVersion> expectedVersions)
        {

            if (events.Count == 0)
            {
                return expectedVersions;
            }

            var userEvents = events.Any(x => !(x is UserLoginEvent));
            var loginEvents = events.Any(x => x is UserLoginEvent);
            if (userEvents && loginEvents)
            {
                throw new InvalidOperationException("cannot save login and user events at the same time");
            }

            var streamName = loginEvents ? LoginStream(identifier) : UserStream(identifier);

            var sv = expectedVersions.Single(x => x.Stream.StartsWith(streamName));

            if (loginEvents)
            {
                var uv = expectedVersions.Single(x => x.Stream.StartsWith("user-"));
                foreach (var e in events)
                {
                    e.AddMetadataValue("userVersion", uv.Version);
                }
            }

            var result = await _store.Append(sv.Stream, sv.Version, events.Select(x => _eventSerializer.Serialize(identifier, x)).ToArray());

            return new List<StreamVersion>(expectedVersions.Where(x => x != sv)) {
                new StreamVersion(streamName, result)
            };
        }
    }
}