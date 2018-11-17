using AggregateConsistency.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregateConsistency
{
	internal static class KeyStoreExtensions
	{
		public static Preprocessor DecryptFactory(this IKeyStore store, string scopeIdentity)
		{
			return new Wrapper(store, scopeIdentity).TryDecrypt;
		}

		private struct Wrapper
		{
			private readonly IKeyStore _store;
			private readonly string _scopeIdentity;

			public Wrapper(IKeyStore store, string scopeIdentity)
			{
				_store = store;
				_scopeIdentity = scopeIdentity;
			}

			public bool TryDecrypt(byte[] input, out byte[] output)
			{
				return _store.TryDecrypt(_scopeIdentity, input, out output);
			}
		}
	}

	internal class Program
	{
		private static void Main(string[] args)
		{
			var serializationRegistry = new SerializationRegistry();
			var keyStore = new ReallyInsecureAndVeryTemporaryKeyStore();
			keyStore.CreateKeyIfNotExists("test");
			serializationRegistry.DefaultForEvent<Foo>(s => d => keyStore.Encrypt(s, d), keyStore.DecryptFactory);
			var ks = serializationRegistry.Build();
			var serialized = ks.Events.Serializer.Serialize("test", new Foo(Guid.NewGuid(), "user@example.com"));
			var stored = new SerializedEvent(serialized.Id, serialized.Type, serialized.Data, serialized.Metadata, "test", serialized.StreamRevision);
			Event @event = null;
			var count = 0;
			ks.Events.Deserializer.Deserialize("test", typeof(object), new Dictionary<string, long>(),
				() =>
				{
					if (count++ == 0)
					{
						return Task.FromResult(stored);
					}

					return Task.FromResult(default(SerializedEvent));
				}, e => @event = e).Wait();
			Console.WriteLine(((Foo)@event).Email);
		}

		private class Foo : Event
		{
			public string Email { get; }

			public Foo(Guid id, string email) : base(id)
			{
				Email = email;
			}
		}
	}

	static class UserBoundedContext
	{
		public static SerializationRegistry RegisterSerializers(SerializationRegistry registry, IKeyStore store)
		{
			return registry;
		}

		public static void RegisterCommands(ICommandRegistry registry, UserRepository repository)
		{
			var handler = new UserCommandHandler(registry.For<User>(), repository);
		}
	}

	public enum LoginResult
	{
		Failed,
		Success,
		LockedOut,
	}
	public class UserCommandHandler
	{
		public UserCommandHandler(ICommandRegistry<User> registry, UserRepository repository)
		{

			registry
				.Execute<LoginUser,LoginResult>(Login, id => repository.Load(id, true), repository.Save)
				.WithDefaults(repository.SaveNew, id => repository.Load(id),
					repository.Save)
				.Create<CreateUser>(CreateUser)
				.Execute<VerifyEmailAndSetPassword>(VerifyEmailAndSetPassword);
		}

		User CreateUser(CreateUser cmd)
		{
			return new User();
		}

		LoginResult Login(LoginUser cmd, User user)
		{
			return LoginResult.Failed;
		}

		void VerifyEmailAndSetPassword(VerifyEmailAndSetPassword cmd, User user)
		{}
	}

	class CreateUser : UserCommand{
		public CreateUser(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class VerifyEmailAndSetPassword : UserCommand{
		public VerifyEmailAndSetPassword(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class ChangeEmail : UserCommand{
		public ChangeEmail(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class VerifyEmail : UserCommand{
		public VerifyEmail(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class ChangePassword : UserCommand{
		public ChangePassword(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class RequestResetPassword : UserCommand{
		public RequestResetPassword(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class SetPassword : UserCommand{
		public SetPassword(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class LoginUser : UserCommand{
		public LoginUser(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class ManualUnlock : UserCommand{
		public ManualUnlock(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}
	class ManualLock : UserCommand{
		public ManualLock(Guid commandId, string userId) : base(commandId, userId)
		{
		}
	}

	public abstract class UserCommand : Command
	{
		public string UserId { get; }

		protected UserCommand(Guid commandId, string userId) : base(commandId, userId) {
			UserId = userId;
		}
	}
	public class User : Aggregate { }
	public abstract class UserEvent : Event
	{
		protected UserEvent(Guid id) : base(id)
		{
		}
	}

	public abstract class UserLoginEvent : Event
	{
		protected UserLoginEvent(Guid id) : base(id)
		{
		}
	}

	public class UserRepository : Repository
	{

		private static readonly IReadOnlyDictionary<long, IReadOnlyList<Event>> NoLoginEvents =
			new Dictionary<long, IReadOnlyList<Event>>();

		public UserRepository(IEventStore store,
			IKnownSerializers serializers) : base(store, serializers)
		{
		}

		/// <summary>
		/// Loads a user from an event store, including login events if specified.
		/// Usually only including login events for login operations, passed in by method calls in command handlers.
		/// Reads all events for the user stream, and populates the aggregate.
		/// </summary>
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

