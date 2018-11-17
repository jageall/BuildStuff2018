using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AggregateConsistency.Infrastructure
{
    public delegate byte[] Postprocessor(byte[] data);
    public delegate Postprocessor CreatePostprocessor(string scopeIdentifier);
    public delegate bool Preprocessor(byte[] input, out byte[] output);
    public delegate Preprocessor CreatePreprocessor(string identifier);
	public static class SerializationRegistryExtensions
	{
		public static ISerializationRegistry DefaultForEvent<T>(this ISerializationRegistry registry, CreatePostprocessor postSerialization = null, CreatePreprocessor preDeserialization = null)
			where T : Event
		{
			bool prePostIsValidEnough = (postSerialization != null && preDeserialization != null) ||
			                            (postSerialization == null && preDeserialization == null);
			if(!prePostIsValidEnough) throw new ArgumentException("Both pre and post serialization must be specified or neither");
			return registry
				.RegisterEventSerializer<T>(CamelCase(typeof(T).Name), 1,
					(e, m) => JObject.FromObject(e, SerializationRegistry.DefaultSerializer), postSerialization)
				.RegisterEventDeserializer(typeof(object), CamelCase(typeof(T).Name), 1,
					(json, m) => {
						var e = json.ToObject<T>(SerializationRegistry.DefaultSerializer);
						var em = (IEvent) e;
						em.Metadata = (IMetadata) m;
						return e;
					}, preDeserialization);
		}

		public static ISerializationRegistry DefaultForSnapshot<T>(this ISerializationRegistry registry)
			where T : class {
			return registry
				.RegisterSnapshotSerializer<T>((e, m) => JObject.FromObject(e, SerializationRegistry.DefaultSerializer))
				.RegisterSnapshotDeserializer((json, _) => json.ToObject<T>(SerializationRegistry.DefaultSerializer));
		}

		public static ISerializationRegistry RegisterSnapshotSerializer<T>(this ISerializationRegistry registry,
			Func<T, IMetadata, JObject> serializer) {
			var r = (IRegisterSnapshotSerializers) registry;
			r.Register(serializer);
			return registry;
		}

		public static ISerializationRegistry RegisterSnapshotDeserializer<T>(this ISerializationRegistry registry,
			Func<JObject, IReadOnlyMetadata, T> deserializer) {
			var r = (IRegisterSnapshotDeserializers) registry;
			r.Register(deserializer);
			return registry;
		}

		public static ISerializationRegistry RegisterEventSerializer<T>(
			this ISerializationRegistry registry,
			string typeName,
			int version,
			Func<T, IMetadata, JObject> serializer, 
			CreatePostprocessor postprocessor = null
		) where T : Event {
			var r = (IRegisterEventSerializers) registry;
			r.Register(typeName, version, serializer, postprocessor);
			return registry;
		}

		public static ISerializationRegistry RegisterEventDeserializer(
			this ISerializationRegistry registry,
			Type scope,
			string type,
			int version,
			Func<JObject, IReadOnlyMetadata, Event> deserializer,
			CreatePreprocessor preprocessor = null) {
			var r = (IRegisterEventDeserializers) registry;
			r.Register(scope, type, version, (json, metadata) => new[] {deserializer(json, metadata)}, preprocessor);
			return registry;
		}

		static string CamelCase(string value) {
			if(char.IsLower(value, 0))
				return value;
			var chars = value.ToCharArray();
			chars[0] = char.ToLowerInvariant(chars[0]);
			return new string(chars);
		}

	    public static ISerializationRegistry WithEncryption(this ISerializationRegistry registry, IKeyStore store)
	    {
            return new WithEncryptionRegistry(registry, store);
	    }

        class WithEncryptionRegistry: ISerializationRegistry {
            private readonly ISerializationRegistry _registry;
            private readonly IKeyStore _store;
            private CreatePostprocessor _postprocessorFactory;
            private CreatePreprocessor _preprocessorFactory;

            public WithEncryptionRegistry(ISerializationRegistry registry, IKeyStore store)
            {
                _registry = registry;
                _store = store;
                _postprocessorFactory = s => d => store.Encrypt(s, d);
                _preprocessorFactory = store.DecryptFactory;
            }

            public void Register(Type scope, string name, int version, Func<JObject, IReadOnlyMetadata, IEnumerable<Event>> deserializer, CreatePreprocessor preprocessor)
            {
                _registry.Register(scope, name, version, deserializer, _preprocessorFactory);
            }

            public void Register<T>(string typeName, int version, Func<T, IMetadata, JObject> serializer, CreatePostprocessor postProcessor) where T : Event
            {
                _registry.Register(typeName, version, serializer, _postprocessorFactory);
            }

            public void Register<T>(Func<JObject, IReadOnlyMetadata, T> serializer)
            {
                _registry.Register(serializer);
            }

            public void Register<T>(Func<T, IMetadata, JObject> serializer)
            {
                _registry.Register(serializer);
            }
        }
	}

    public interface ISerializationRegistry
    : IRegisterEventDeserializers, IRegisterEventSerializers,
        IRegisterSnapshotDeserializers, IRegisterSnapshotSerializers
    {

    }
}