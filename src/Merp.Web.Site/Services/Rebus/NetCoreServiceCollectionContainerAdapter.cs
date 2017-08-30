﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Merp.Web.Site.Services.Rebus
{
    /// <summary>
    /// Implementation of <see cref="IContainerAdapter"/> that is backed by a ServiceProvider
    /// </summary>
    /// <seealso cref="Rebus.Activation.IContainerAdapter" />
    public class NetCoreServiceCollectionContainerAdapter : IContainerAdapter
    {
        readonly IServiceCollection _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetCoreServiceCollectionContainerAdapter"/> class.
        /// </summary>
        /// <param name="services">The ServiceCollection.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public NetCoreServiceCollectionContainerAdapter(IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            _services = services;

            var serviceProvider = _services.BuildServiceProvider();
            var applicationLifetime = serviceProvider.GetService<IApplicationLifetime>();

            if (applicationLifetime != null)
            {
                applicationLifetime.ApplicationStopping.Register(DisposeBus);
            }
        }

        /// <summary>
        /// Resolves all handlers for the given <typeparamref name="TMessage"/> message type
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="message"></param>
        /// <param name="transactionContext"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IHandleMessages<TMessage>>> GetHandlers<TMessage>(TMessage message, ITransactionContext transactionContext)
        {
            var resolvedHandlerInstances = GetAllHandlersInstances<TMessage>();

            transactionContext.OnDisposed(() =>
            {
                foreach (var disposableInstance in resolvedHandlerInstances.OfType<IDisposable>())
                {
                    disposableInstance.Dispose();
                }
            });

            return resolvedHandlerInstances;
        }

        /// <summary>
        /// Sets the bus instance that this <see cref="T:Rebus.Activation.IContainerAdapter" /> should be able to inject when resolving handler instances
        /// </summary>
        /// <param name="bus"></param>
        public void SetBus(IBus bus)
        {
            _services.AddSingleton<IBus>(bus);
            _services.AddTransient<IMessageContext>((s) => MessageContext.Current);
        }

        void DisposeBus()
        {
            var serviceProvider = _services.BuildServiceProvider();
            var bus = serviceProvider.GetService<IBus>();

            bus.Dispose();
        }

        List<IHandleMessages<TMessage>> GetAllHandlersInstances<TMessage>()
        {
            var container = _services.BuildServiceProvider();

            var handledMessageTypes = typeof(TMessage).GetBaseTypes()
                .Concat(new[] { typeof(TMessage) });

            return handledMessageTypes
                .SelectMany(t =>
                {
                    var implementedInterface = typeof(IHandleMessages<>).MakeGenericType(t);

                    return container.GetServices(implementedInterface).Cast<IHandleMessages>();
                })
                .Cast<IHandleMessages<TMessage>>()
                .ToList();
        }
    }
}
