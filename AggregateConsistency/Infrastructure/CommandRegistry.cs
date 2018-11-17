using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AggregateConsistency.Infrastructure
{
	public class CommandRegistry : ICommandRegistry
	{
		private readonly Dictionary<Type, Func<UnitOfWork, ICommand, Task<UnitOfWork>>> _handlers;

		public CommandRegistry() {
			_handlers = new Dictionary<Type, Func<UnitOfWork, ICommand, Task<UnitOfWork>>>();
		}

		public ICommandHandler Build() {
			return new CommandHandler(_handlers);
		}

		ICommandRegistry ICommandRegistry.Create<TCommand, TAggregate>(Func<TCommand, TAggregate> creator,
			Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> save) {
			EnsureUniqueCommand<TCommand>();

			Task<UnitOfWork> Dispatch(UnitOfWork uow, ICommand cmd) {
				if(uow != null) {
					throw new InvalidOperationException("Cannot execute create command with existing unit of work");
				}
				var created = creator((TCommand) cmd);

				return Task.FromResult(new UnitOfWork(created, save, Result.Nothing));
			}

			_handlers.Add(typeof(TCommand), Dispatch);
			return this;
		}

		void EnsureUniqueCommand<TCommand>() {
			if(_handlers.ContainsKey(typeof(TCommand)))
				throw new InvalidOperationException($"Command handler for {typeof(TCommand).FullName} has already been added");
		}

		ICommandRegistry ICommandRegistry.Execute<TCommand, TAggregate>(Action<TCommand, TAggregate> action,
			Func<string, Task<LoadResult<TAggregate>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save) {
			EnsureUniqueCommand<TCommand>();

			async Task<UnitOfWork> Dispatch(UnitOfWork uow, ICommand cmd) {
				if(uow == null) {
					var loaded = await load(cmd.TargetIdentifier);
					uow = new UnitOfWork(loaded.Loaded, (id, events) => save(id, events, loaded.ExpectedVersions), Result.Nothing);
				}
				else if(!(uow.Aggregate is TAggregate))
					throw new InvalidOperationException("wrong aggregate type");

				action((TCommand) cmd, (TAggregate) uow.Aggregate);
				return uow;
			}

			_handlers.Add(typeof(TCommand), Dispatch);
			return this;
		}

		ICommandRegistry ICommandRegistry.Execute<TCommand, TAggregate, TResult>(Func<TCommand, TAggregate, TResult> action,
			Func<string, Task<LoadResult<TAggregate>>> load,
			Func<string, IReadOnlyList<Event>, IReadOnlyList<StreamVersion>, Task<IReadOnlyList<StreamVersion>>> save) {
			EnsureUniqueCommand<TCommand>();

			async Task<UnitOfWork> Dispatch(UnitOfWork uow, ICommand cmd) {
				if(uow == null) {
					var loaded = await load(cmd.TargetIdentifier);
					uow = new UnitOfWork(loaded.Loaded, (id, events) => save(id, events, loaded.ExpectedVersions), Result.Nothing);
				}
				else if(!(uow.Aggregate is TAggregate))
					throw new InvalidOperationException("wrong aggregate type");

				var result = action((TCommand) cmd, (TAggregate) uow.Aggregate);
				return uow.With(cmd, result);
			}

			_handlers.Add(typeof(TCommand), Dispatch);
			return this;
		}

		class CommandHandler : ICommandHandler
		{

			readonly Dictionary<Type, Func<UnitOfWork, ICommand, Task<UnitOfWork>>> _handlers;

			public CommandHandler(Dictionary<Type, Func<UnitOfWork, ICommand, Task<UnitOfWork>>> handlers) {
				_handlers = handlers;
			}

			public async Task<Result> Execute(ICommand cmd) {
				var cmds = cmd as IEnumerable<ICommand> ?? new[] {cmd};
				UnitOfWork uow = null;
				var target = cmd.TargetIdentifier;

				foreach(var command in cmds) {
					if(command.TargetIdentifier != target) {
						throw new InvalidOperationException("All commands in a batch must have the same target identifier");
					}

					Func<UnitOfWork, ICommand, Task<UnitOfWork>> handler;
					if(!_handlers.TryGetValue(cmd.GetType(), out handler)) {
						throw new InvalidOperationException("no handler registered for command type");
					}

					uow = await handler(uow, command);
				}

				if(uow == null) return Result.Nothing;

				// execute save for the unit of work and related events
				// cannot write to multiple streams, repository will throw
				var pendingEvents = uow.Aggregate.PendingEvents();
				if(pendingEvents.Count > 0) {
					await uow.Save(cmd.TargetIdentifier, pendingEvents);
				}
				return uow.Result;
			}
		}

		/// <summary>
		/// Internalizes the unit of work pattern. Do not touch. Or, touch at own risk. Things have to work.
		/// DRAGONS.
		/// </summary>
		class UnitOfWork
		{
			public UnitOfWork(Aggregate aggregate, Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> save, Result result) {
				Aggregate = aggregate;
				Save = save;
				Result = result;
			}

			public Aggregate Aggregate { get; }
			public Func<string, IReadOnlyList<Event>, Task<IReadOnlyList<StreamVersion>>> Save { get; }
			public Result Result { get; }

			public UnitOfWork With<T>(ICommand command, T result) {
				return new UnitOfWork(Aggregate, Save, Result.With<T>(command, result));
			}
		}
	}
}