using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace AggregateConsistency.Infrastructure
{
	public class EventDispatcher
	{
		static readonly MethodInfo OpenMethod = typeof(EventDispatcher).GetTypeInfo().GetMethod("For", Type.EmptyTypes);
		readonly IReadOnlyDictionary<Type, Action<object, Event>> _dispatchers;
		readonly Type _dispatcherType;

		EventDispatcher(Type dispatcherType, IReadOnlyDictionary<Type, Action<object, Event>> dispatchers) {
			_dispatcherType = dispatcherType;
			_dispatchers = dispatchers;
		}

		public static EventDispatcher For(Type type) {
			var closed = OpenMethod.MakeGenericMethod(type);
			return (EventDispatcher) closed.Invoke(null, null);
		}

		public static EventDispatcher For<T>() {
			return Dispatcher<T>.Instance;
		}

		/// <summary>
		/// Dispatches events to the correct apply method on the target aggregate.
		/// </summary>
		/// <param name="target">The aggregate to apply the event to</param>
		/// <param name="event">The event to apply</param>
		public void Dispatch(object target, Event @event) {
			if(target == null) throw new ArgumentNullException(nameof(target));
			if(@event == null) throw new ArgumentNullException(nameof(@event));
			if(target.GetType() != _dispatcherType)
				throw new ArgumentException(
					$"Cannot dispatch events for type {target.GetType()} using dispatcher for {_dispatcherType}");

			Action<object, Event> dispatcher;
			if(!_dispatchers.TryGetValue(@event.GetType(), out dispatcher)) {
				throw new InvalidOperationException(
					$"No dispatchers registered for {@event.GetType()} on {_dispatcherType}");
			}
			dispatcher(target, @event);
		}

		static class Dispatcher<T>
		{
			public static readonly EventDispatcher Instance = new EventDispatcher(typeof(T), CreateDispatchers());

			/// <summary>
			/// Uses reflection to build event dispatch methods based on convention of method name being Apply
			/// returning void and only having one parameter that inherits from Event
			/// </summary>
			/// <returns></returns>
			static IReadOnlyDictionary<Type, Action<object, Event>> CreateDispatchers() {
				var dispatchers = new Dictionary<Type, Action<object, Event>>();
				var targetType = typeof(T);
				var eventType = typeof(Event);
				var objectType = typeof(object);
				var target = Expression.Parameter(objectType, "target");
				var @event = Expression.Parameter(eventType, "event");
				foreach(var m in targetType.GetRuntimeMethods()
					.Select(x => new {
						x.Name,
						x.ReturnType,
						Parameters = x.GetParameters(),
						Method = x
					})
					.Where(x =>
						x.Name == "Apply" &&
						x.ReturnType == typeof(void)
						&& x.Parameters.Length == 1
						&& x.Parameters[0].ParameterType != typeof(Event)
						&& typeof(Event).GetTypeInfo().IsAssignableFrom(x.Parameters[0].ParameterType))
					.Select(x => new {
						x.Method,
						EventType = x.Parameters[0].ParameterType
					})) {
					var body = Expression.Call(Expression.ConvertChecked(target, targetType), m.Method,
						Expression.ConvertChecked(@event, m.EventType));

					var dispatcher = Expression.Lambda<Action<object, Event>>(body, target, @event).Compile();
					dispatchers.Add(m.EventType, dispatcher);
				}
				return dispatchers;
			}
		}
	}
}