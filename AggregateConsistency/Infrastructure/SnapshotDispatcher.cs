using System;
using System.Linq;
using System.Reflection;

namespace AggregateConsistency.Infrastructure
{
	public abstract class SnapshotDispatcher
	{
		private static readonly MethodInfo OpenMethod = typeof(SnapshotDispatcher).GetTypeInfo().GetMethod("For", Type.EmptyTypes);

		private static readonly SnapshotDispatcher NullDispatcher = new NoSnapshot();
		public abstract bool IsSnapshottable { get; }
		public abstract Type SnapshotType { get; }
		public abstract SnapshotStream SnapshotStream(string originStream);

		public static SnapshotDispatcher For(Type type) {
			var closed = OpenMethod.MakeGenericMethod(type);
			return (SnapshotDispatcher) closed.Invoke(null, null);
		}

		public static SnapshotDispatcher For<T>() {
			return Singleton<T>.Instance;
		}

		public abstract bool Apply(object target, object snapshot);
		public abstract object Take(object target);

		private class Singleton<T>
		{
			public static readonly SnapshotDispatcher Instance = CreateDispatcher();

			private static SnapshotDispatcher CreateDispatcher() {
				var snapshotInterface =
					typeof(T).GetTypeInfo().GetInterfaces()
						.SingleOrDefault(
							x => x.IsConstructedGenericType && x.GetGenericTypeDefinition() == typeof(ISnapshot<>));
				if(snapshotInterface == null) return NullDispatcher;
				var snapshotType = snapshotInterface.GenericTypeArguments[0];
				return
					(SnapshotDispatcher)
					Activator.CreateInstance(typeof(Dispatcher<,>).MakeGenericType(typeof(T), snapshotType));
			}
		}

		private class Dispatcher<TTarget, TSnapshot> : SnapshotDispatcher where TTarget : class, ISnapshot<TSnapshot>
			where TSnapshot : class
		{
			public override bool IsSnapshottable => true;
			public override Type SnapshotType => typeof(TSnapshot);

			public override SnapshotStream SnapshotStream(string originStream) {
				return new SnapshotStream(originStream, SnapshotType);
			}

			public override bool Apply(object target, object snapshot) {
				var t = target as TTarget;
				var s = snapshot as TSnapshot;
				if(t == null || s == null) return false;
				t.ApplySnapshot(s);
				return true;
			}

			public override object Take(object target) {
				var t = target as TTarget;
				return t?.TakeSnapshot();
			}
		}

		private class NoSnapshot : SnapshotDispatcher
		{
			public override bool IsSnapshottable => false;
			public override Type SnapshotType => null;

			public override SnapshotStream SnapshotStream(string originStream) {
				return new SnapshotStream(originStream, SnapshotType);
			}

			public override bool Apply(object target, object snapshot) {
				return false;
			}

			public override object Take(object target) {
				return null;
			}
		}
	}
}