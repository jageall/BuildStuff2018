using AggregateConsistency.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AggregateConsistency
{
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

			public Foo(Guid id, string email) : base()
            {
				Email = email;
			}
		}
	}
}

