using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public class SerializationRegistry :
		IRegisterEventDeserializers,
		IRegisterEventSerializers,
		IRegisterSnapshotSerializers,
		IRegisterSnapshotDeserializers
	{
		private class CamelCaseExceptDictionaryKeysResolver : CamelCasePropertyNamesContractResolver
		{
			protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
			{
				var contract = base.CreateDictionaryContract(objectType);
				contract.DictionaryKeyResolver = key => key;
				return contract;
			}
		}

		internal static readonly JsonSerializer DefaultSerializer = new JsonSerializer
		{
			ContractResolver = new CamelCaseExceptDictionaryKeysResolver(),
			DateParseHandling = DateParseHandling.DateTimeOffset,
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			ReferenceLoopHandling = ReferenceLoopHandling.Error,
		};
		private readonly Dictionary
			<Type,
				Dictionary<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>>>
			_eventDeserializers;
		private readonly Dictionary<Type, Func<string, Event, Metadata, byte[]>> _eventSerializers;
		private readonly Dictionary<Type, Func<JObject, IReadOnlyMetadata, object>> _snapshotDeserializers;
		private readonly Dictionary<Type, Func<object, Metadata, JObject>> _snapshotSerializers;

		public SerializationRegistry()
		{
			_eventDeserializers =
				new Dictionary
				<Type,
					Dictionary
					<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>>>();
			_eventSerializers = new Dictionary<Type, Func<string, Event, Metadata, byte[]>>();
			_snapshotDeserializers = new Dictionary<Type, Func<JObject, IReadOnlyMetadata, object>>();
			_snapshotSerializers = new Dictionary<Type, Func<object, Metadata, JObject>>();

		}

        private static Event[] EmptyEventArray = new Event[] { };
		void IRegisterEventDeserializers.Register(Type scope, string type, int version,
			Func<JObject, IReadOnlyMetadata, IEnumerable<Event>> deserializer, CreatePreprocessor preprocessor)
		{
			preprocessor = preprocessor ?? DefaultPreprocessor;

			IEnumerable<Event> Deserialize(string scopeIdentity, byte[] data, IReadOnlyMetadata metadata)
			{
                if (!preprocessor(scopeIdentity)(data, out var processed))
                    return EmptyEventArray;
				var json = ReadJson(processed);
				return deserializer(json, metadata);
			}
			if (!_eventDeserializers.TryGetValue(scope, out var deserializers))
			{
				deserializers =
					new Dictionary
						<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>>();
				_eventDeserializers[scope] = deserializers;
			}
			var tv = new TypeAndVersion(type, version);
			if (deserializers.ContainsKey(tv))
			{
				throw new InvalidOperationException(
					$"Deserializer already registered for {type} with {version} in scope {(scope == typeof(object) ? "(global)" : scope.FullName)}");
			}

			deserializers.Add(tv, Deserialize);
		}

		private static readonly Postprocessor PostProcessorPassThrough = x => x;
		private static readonly CreatePostprocessor DefaultPostprocessor = _ => PostProcessorPassThrough;
        private static bool PreProcessorPassThrough(byte[] input, out byte[] data)
        {
            data = input;
            return true;
        }
        private static readonly CreatePreprocessor DefaultPreprocessor = _ => PreProcessorPassThrough;

		void IRegisterEventSerializers.Register<T>(string typeName, int version, Func<T, IMetadata, JObject> serializer, CreatePostprocessor postProcessor)
		{
			postProcessor = postProcessor ?? DefaultPostprocessor;
			byte[] Serialize(string id, Event e, Metadata m)
			{
				m.Type = typeName;
				m.Version = version;
				var json = serializer((T)e, m);
				var bytes = json.ToBytes();
				return postProcessor(id)(bytes);
			}

			if (_eventSerializers.ContainsKey(typeof(T)))
			{
				throw new InvalidOperationException($"Serializer already registered for {typeof(T)}");
			}

			_eventSerializers.Add(typeof(T), Serialize);
		}

		void IRegisterSnapshotDeserializers.Register<T>(Func<JObject, IReadOnlyMetadata, T> deserializer)
		{
			if (_snapshotDeserializers.ContainsKey(typeof(T)))
			{
				throw new InvalidOperationException(
					$"Snapshot deserializer for type name {typeof(T)} already registered");
			}
			_snapshotDeserializers.Add(typeof(T), (json, m) =>
			{
				try
				{
					return deserializer(json, m);
				}
				catch
				{
					return null;
				}
			});
		}

		void IRegisterSnapshotSerializers.Register<T>(Func<T, IMetadata, JObject> serializer)
		{
			if (_snapshotSerializers.ContainsKey(typeof(T)))
			{
				throw new InvalidOperationException(
					$"Snapshot deserializer for type name {typeof(T)} already registered");
			}
			_snapshotSerializers.Add(typeof(T), (o, m) =>
			{
				try
				{
					return serializer((T)o, m);
				}
				catch
				{
					return null;
				}
			});
		}

		public IKnownSerializers Build()
		{
			var eventDeserializers =
				new Dictionary
				<Type,
					IReadOnlyDictionary
					<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>>>();
			foreach (var kvp in _eventDeserializers)
			{
				eventDeserializers.Add(kvp.Key, kvp.Value);
			}

			return new KnownSerializers(
				new EventSerializer(_eventSerializers),
				new EventDeserializer(eventDeserializers),
				new SnapshotSerializer(_snapshotSerializers),
				new SnapshotDeserializer(_snapshotDeserializers));
		}

		private static readonly JObject EmptyJObject = new JObject();

		private static JObject ReadJson(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return EmptyJObject;
			}

			var ms = new MemoryStream(bytes);

			using (var jr = new JsonTextReader(new StreamReader(ms)) { DateParseHandling = DateParseHandling.None })
			{
				var json = (JObject)JToken.ReadFrom(jr);
				return json;
			}
		}

		private class KnownSerializers : IKnownSerializers
		{
			public KnownSerializers(IEventSerializer eventSerializer, IEventDeserializer eventDeserializer,
				ISnapshotSerializer snapshotSerializer, ISnapshotDeserializer snapshotDeserializer)
			{
				Events = new SerializationPair<IEventSerializer, IEventDeserializer>(eventSerializer, eventDeserializer);
				Snapshots = new SerializationPair<ISnapshotSerializer, ISnapshotDeserializer>(snapshotSerializer,
					snapshotDeserializer);
			}

			public ISerializationPair<IEventSerializer, IEventDeserializer> Events { get; }
			public ISerializationPair<ISnapshotSerializer, ISnapshotDeserializer> Snapshots { get; }

			private class SerializationPair<TSerializer, TDeserializer> : ISerializationPair<TSerializer, TDeserializer>
			{
				public SerializationPair(TSerializer serializer, TDeserializer deserializer)
				{
					Serializer = serializer;
					Deserializer = deserializer;
				}

				public TSerializer Serializer { get; }
				public TDeserializer Deserializer { get; }
			}
		}

		private class EventDeserializer : IEventDeserializer
		{
			private readonly IReadOnlyDictionary
				<Type,
					IReadOnlyDictionary
					<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>>>
				_deserializers;

			public EventDeserializer(
				IReadOnlyDictionary
					<Type,
						IReadOnlyDictionary
						<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>>>
					deserializers)
			{
				_deserializers = deserializers;
			}

			public async Task Deserialize(
				string scopeIdentity,
				Type scopeType,
				Dictionary<string, long> streamRevisions,
				Func<Task<SerializedEvent>> events,
				Action<Event> onEvent)
			{
				var scope = GetScope(scopeType);
				var next = await events();

				while (next.IsValid)
				{
					var currentRevision = next.StreamRevision;
					UpdateStreamRevision(streamRevisions, next, currentRevision);
					foreach (
						var @event in
						Deserialize(scopeIdentity, scope, next))
					{
						onEvent(@event);
					}
					next = await events();
				}
			}

			private IReadOnlyDictionary<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>> GetScope(Type scopeType)
			{
				if (!_deserializers.TryGetValue(scopeType, out var scope))
				{
					scope = _deserializers[typeof(object)];
				}
				return scope;
			}

			private static void UpdateStreamRevision(Dictionary<string, long> streamRevisions, SerializedEvent next, long currentRevision)
			{
				if (!streamRevisions.TryGetValue(next.Stream, out var maxRevision) || maxRevision < currentRevision)
				{
					streamRevisions[next.Stream] = next.StreamRevision;
				}
			}

			private IEnumerable<Event> Deserialize(
				string scopeIdentity,
				IReadOnlyDictionary
					<TypeAndVersion, Func<string, byte[], IReadOnlyMetadata, IEnumerable<Event>>>
					scope, SerializedEvent serialized)
			{
				Metadata metadata = new Metadata(ReadJson(serialized.Metadata), serialized.StreamRevision);
				if (scope.TryGetValue(new TypeAndVersion(serialized.Type, metadata.Version), out var deserializer))
				{
					foreach (var @event in deserializer(scopeIdentity, serialized.Data, metadata))
					{
						yield return @event;
					}
				}
			}


		}
		
		private class EventSerializer : IEventSerializer
		{
			private readonly IReadOnlyDictionary<Type, Func<string, Event, Metadata, byte[]>> _serializers;

			public EventSerializer(IReadOnlyDictionary<Type, Func<string, Event, Metadata, byte[]>> serializers)
			{
				_serializers = serializers;
			}

			public SerializedEvent Serialize(string id, Event @event)
			{
				if (!_serializers.TryGetValue(@event.GetType(), out var serializer))
				{
					throw new InvalidOperationException($"No serializer registered for event type {@event.GetType()}");
				}
				var md = (Metadata)((IEvent)@event).Metadata ?? new Metadata();

				var data = serializer(id, @event, md);
				return new SerializedEvent(@event.Id, md.Type, data, md.ToBytes(), null, 0);
			}
		}

		private class SnapshotSerializer : ISnapshotSerializer
		{
			private readonly IReadOnlyDictionary<Type, Func<object, Metadata, JObject>> _serializers;

			public SnapshotSerializer(IReadOnlyDictionary<Type, Func<object, Metadata, JObject>> serializers)
			{
				_serializers = serializers;
			}

			public SerializedEvent Serialize(object snapshot, long revision)
			{
				if (!_serializers.TryGetValue(snapshot.GetType(), out var serializer))
				{
					throw new Exception($"No snapshot serializer registered for type {snapshot.GetType().FullName}");
				}

				var md = new Metadata();
				md.Write("revision", revision);
				var data = serializer(snapshot, md);
				return new SerializedEvent(Guid.NewGuid(), md.Type, data.ToBytes(), md.ToBytes(), "", 0);
			}
		}

		private class SnapshotDeserializer : ISnapshotDeserializer
		{
			private readonly IReadOnlyDictionary<Type, Func<JObject, IReadOnlyMetadata, object>> _deserializers;

			public SnapshotDeserializer(
				IReadOnlyDictionary<Type, Func<JObject, IReadOnlyMetadata, object>> deserializers)
			{
				_deserializers = deserializers;
			}

			public Snapshot Deserialize(Type type, SerializedEvent value)
			{
				if (!_deserializers.TryGetValue(type, out var deserializer))
				{
					return new Snapshot(-1, value.StreamRevision, null);
				}
				var md = new Metadata(ReadJson(value.Metadata), value.StreamRevision);
				var payload = deserializer(ReadJson(value.Data), md);
				return new Snapshot(md.Read<int>("revision"), value.StreamRevision, payload);
			}
		}

		private struct TypeAndVersion : IEquatable<TypeAndVersion>
		{
			public override bool Equals(object obj)
			{
				return base.Equals(obj);
			}

			public bool Equals(TypeAndVersion other)
			{
				return string.Equals(Type, other.Type) && Version == other.Version;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return ((Type?.GetHashCode() ?? 0) * 397) ^ Version;
				}
			}

			public static bool operator ==(TypeAndVersion left, TypeAndVersion right)
			{
				return left.Equals(right);
			}

			public static bool operator !=(TypeAndVersion left, TypeAndVersion right)
			{
				return !left.Equals(right);
			}

			public TypeAndVersion(string type, int version)
			{
				Type = type;
				Version = version;
			}

			public string Type { get; }

			public int Version { get; }
		}

		private class Metadata : IMetadata
		{
			private readonly JObject _json;
			private readonly bool _isReadonly;

			public Metadata()
			{
				_json = new JObject();
			}

			internal Metadata(JObject json, long streamRevision)
			{
				StreamRevision = streamRevision;
				_json = json;
				_isReadonly = true;
			}

			public string Type { get; set; }
			public long StreamRevision { get; set; }

			public int Version
			{
				get => ReadValueOrDefault<int>("version", -1);
				set
				{
					if (value < 1)
					{
						throw new ArgumentOutOfRangeException(nameof(value), value, "version must be >= 1");
					}

					Write("version", value);
				}
			}

			public T Read<T>(string name)
			{
				return _json.Value<T>(name);
			}

			public T ReadValueOrDefault<T>(string name, T @default = default(T))
			{
				if (TryRead<T>(name, out var result))
				{
					return result;
				}

				return @default;
			}

			public bool TryRead<T>(string name, out T value)
			{
				if (_json.TryGetValue(name, out var token))
				{
					try
					{
						value = token.ToObject<T>();
						return true;
					}
					catch (Exception)
					{
						//TODO: add logging
					}
				}
				value = default(T);
				return false;
			}

			public void Write<T>(string name, T value)
			{
				if (_isReadonly)
				{
					throw new InvalidOperationException("metadata is readonly");
				}

				_json[name] = JToken.FromObject(value, DefaultSerializer);
			}

			public byte[] ToBytes()
			{
				return _json.ToBytes();
			}
		}

		internal static void AddMetadataToEvent(IEvent @event)
		{
			if (@event.Metadata != null)
			{
				throw new InvalidOperationException("event already has metadata");
			}

			@event.Metadata = new Metadata();
		}
	}
}