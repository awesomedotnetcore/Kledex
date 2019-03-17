﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using OpenCqrs.Dependencies;
using OpenCqrs.Domain;
using OpenCqrs.Events;
using Options = OpenCqrs.Configuration.Options;

namespace OpenCqrs.Commands
{
    /// <inheritdoc />
    public class CommandSender : ICommandSender
    {
        private readonly IHandlerResolver _handlerResolver;
        private readonly IEventPublisher _eventPublisher;
        private readonly IEventFactory _eventFactory;
        private readonly IEventStore _eventStore;
        private readonly ICommandStore _commandStore;
        private readonly Options _options;

        private bool PublishEvent(ICommand command) => command.PublishEvents ?? _options.PublishEvents;
        private bool SaveCommand(IDomainCommand command) => command.SaveCommand ?? _options.SaveCommands;

        public CommandSender(IHandlerResolver handlerResolver,
            IEventPublisher eventPublisher,  
            IEventFactory eventFactory,
            IEventStore eventStore, 
            ICommandStore commandStore,
            IOptions<Options> options)
        {
            _eventPublisher = eventPublisher;
            _eventFactory = eventFactory;
            _eventStore = eventStore;
            _commandStore = commandStore;
            _handlerResolver = handlerResolver;
            _options = options.Value;
        }

        /// <inheritdoc />
        public async Task SendAsync<TCommand>(TCommand command) where TCommand : ICommand
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var handler = _handlerResolver.ResolveHandler<ICommandHandlerAsync<TCommand>>();

            var events = await handler.HandleAsync(command);

            var publishEvents = PublishEvent(command);

            foreach (var @event in events)
            {
                var concreteEvent = _eventFactory.CreateConcreteEvent(@event);

                if (publishEvents)
                    await _eventPublisher.PublishAsync(concreteEvent);
            }
        }

        /// <inheritdoc />
        public async Task SendAsync<TCommand, TAggregate>(TCommand command)
            where TCommand : IDomainCommand
            where TAggregate : IAggregateRoot
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var handler = _handlerResolver.ResolveHandler<IDomainCommandHandlerAsync<TCommand>>();

            if (SaveCommand(command))
                await _commandStore.SaveCommandAsync<TAggregate>(command);

            var events = await handler.HandleAsync(command);

            var publishEvents = PublishEvent(command);

            foreach (var @event in events)
            {
                @event.Update(command);
                var concreteEvent = _eventFactory.CreateConcreteEvent(@event);

                await _eventStore.SaveEventAsync<TAggregate>((IDomainEvent)concreteEvent, command.ExpectedVersion);

                if (publishEvents)
                    await _eventPublisher.PublishAsync(concreteEvent);
            }
        }

        /// <inheritdoc />
        public void Send<TCommand>(TCommand command) where TCommand : ICommand
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var handler = _handlerResolver.ResolveHandler<ICommandHandler<TCommand>>();

            var events = handler.Handle(command);

            var publishEvents = PublishEvent(command);

            foreach (var @event in events)
            {
                var concreteEvent = _eventFactory.CreateConcreteEvent(@event);

                if (publishEvents)
                    _eventPublisher.Publish(concreteEvent);
            }
        }

        /// <inheritdoc />
        public void Send<TCommand, TAggregate>(TCommand command) 
            where TCommand : IDomainCommand 
            where TAggregate : IAggregateRoot
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var handler = _handlerResolver.ResolveHandler<IDomainCommandHandler<TCommand>>();

            if (SaveCommand(command))
                _commandStore.SaveCommand<TAggregate>(command);

            var events = handler.Handle(command);

            var publishEvents = PublishEvent(command);

            foreach (var @event in events)
            {
                @event.Update(command);
                var concreteEvent = _eventFactory.CreateConcreteEvent(@event);

                _eventStore.SaveEvent<TAggregate>((IDomainEvent)concreteEvent, command.ExpectedVersion);

                if (publishEvents)
                    _eventPublisher.Publish(concreteEvent);
            }
        }
    }
}
