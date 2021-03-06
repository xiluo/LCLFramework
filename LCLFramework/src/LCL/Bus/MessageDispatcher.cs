﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace LCL.Bus
{
    /// <summary>
    /// Represents the message dispatcher.
    /// </summary>
    public class MessageDispatcher : IMessageDispatcher
    {
        #region Private Fields
        private readonly Dictionary<Type, List<object>> handlers = new Dictionary<Type, List<object>>();
        #endregion

        #region Private Methods
        /// <summary>
        /// Registers the specified handler type to the message dispatcher.
        /// </summary>
        /// <param name="messageDispatcher">Message dispatcher instance.</param>
        /// <param name="handlerType">The type to be registered.</param>
        private static void RegisterType(IMessageDispatcher messageDispatcher, Type handlerType)
        {
            MethodInfo methodInfo = messageDispatcher.GetType().GetMethod("Register", BindingFlags.Public | BindingFlags.Instance);

            var handlerIntfTypeQuery = from p in handlerType.GetInterfaces()
                                       where p.IsGenericType &&
                                       p.GetGenericTypeDefinition().Equals(typeof(IHandler<>))
                                       select p;
            if (handlerIntfTypeQuery != null)
            {
                foreach (var handlerIntfType in handlerIntfTypeQuery)
                {
                    object handlerInstance = Activator.CreateInstance(handlerType);
                    Type messageType = handlerIntfType.GetGenericArguments().First();
                    MethodInfo genericMethodInfo = methodInfo.MakeGenericMethod(messageType);
                    genericMethodInfo.Invoke(messageDispatcher, new object[] { handlerInstance });
                }
            }
        }
        /// <summary>
        /// Registers all the handler types within a given assembly to the message dispatcher.
        /// </summary>
        /// <param name="messageDispatcher">Message dispatcher instance.</param>
        /// <param name="assembly">The assembly.</param>
        private static void RegisterAssembly(IMessageDispatcher messageDispatcher, Assembly assembly)
        {
            foreach (Type type in assembly.GetExportedTypes())
            {
                var intfs = type.GetInterfaces();
                if (intfs.Any(p =>
                    p.IsGenericType &&
                    p.GetGenericTypeDefinition().Equals(typeof(IHandler<>))) &&
                    intfs.Any(p =>
                    p.IsDefined(typeof(RegisterDispatchAttribute), true)))
                {
                    RegisterType(messageDispatcher, type);
                }
            }
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Occurs when the message dispatcher is going to dispatch a message.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnDispatching(MessageDispatchEventArgs e)
        {
            var temp = this.Dispatching;
            if (temp != null)
                temp(this, e);
        }
        /// <summary>
        /// Occurs when the message dispatcher failed to dispatch a message.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnDispatchFailed(MessageDispatchEventArgs e)
        {
            var temp = this.DispatchFailed;
            if (temp != null)
                temp(this, e);
        }
        /// <summary>
        /// Occurs when the message dispatcher has finished dispatching the message.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnDispatched(MessageDispatchEventArgs e)
        {
            var temp = this.Dispatched;
            if (temp != null)
                temp(this, e);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Creates a message dispatcher and registers all the message handlers
        /// specified in the <see cref="Apworks.Config.IConfigSource"/> instance.
        /// </summary>
        /// <param name="configSource">The <see cref="Apworks.Config.IConfigSource"/> instance
        /// that contains the definitions for message handlers.</param>
        /// <param name="messageDispatcherType">The type of the message dispatcher.</param>
        /// <param name="args">The arguments that is used for initializing the message dispatcher.</param>
        /// <returns>A <see cref="Apworks.Bus.IMessageDispatcher"/> instance.</returns>
        public static IMessageDispatcher CreateAndRegister(string assemblyString)
        {
            IMessageDispatcher messageDispatcher = new MessageDispatcher();

            Assembly assembly = Assembly.Load(assemblyString);
            RegisterAssembly(messageDispatcher, assembly);

            return messageDispatcher;
        }
        #endregion

        #region IMessageDispatcher Members
        /// <summary>
        /// Clears the registration of the message handlers.
        /// </summary>
        public virtual void Clear()
        {
            handlers.Clear();
        }
        /// <summary>
        /// Dispatches the message.
        /// </summary>
        /// <param name="message">The message to be dispatched.</param>
        public virtual void DispatchMessage<T>(T message)
        {
            Type messageType = typeof(T);
            if (handlers.ContainsKey(messageType))
            {
                var messageHandlers = handlers[messageType];
                foreach (var messageHandler in messageHandlers)
                {
                    var dynMessageHandler = (IHandler<T>)messageHandler;
                    var evtArgs = new MessageDispatchEventArgs(message, messageHandler.GetType(), messageHandler);
                    this.OnDispatching(evtArgs);
                    try
                    {
                        dynMessageHandler.Handle(message);
                        this.OnDispatched(evtArgs);
                    }
                    catch
                    {
                        this.OnDispatchFailed(evtArgs);
                    }
                }
            }
        }
        /// <summary>
        /// Registers a message handler into message dispatcher.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="handler">The handler to be registered.</param>
        public virtual void Register<T>(IHandler<T> handler)
        {
            Type keyType = typeof(T);

            if (handlers.ContainsKey(keyType))
            {
                List<object> registeredHandlers = handlers[keyType];
                if (registeredHandlers != null)
                {
                    if (!registeredHandlers.Contains(handler))
                        registeredHandlers.Add(handler);
                }
                else
                {
                    registeredHandlers = new List<object>();
                    registeredHandlers.Add(handler);
                    handlers.Add(keyType, registeredHandlers);

                }
            }
            else
            {
                List<object> registeredHandlers = new List<object>();
                registeredHandlers.Add(handler);
                handlers.Add(keyType, registeredHandlers);
            }
        }
        /// <summary>
        /// Unregisters a message handler from the message dispatcher.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="handler">The handler to be registered.</param>
        public virtual void UnRegister<T>(IHandler<T> handler)
        {
            var keyType = typeof(T);
            if (handlers.ContainsKey(keyType) &&
                handlers[keyType] != null &&
                handlers[keyType].Count > 0 &&
                handlers[keyType].Contains(handler))
            {
                handlers[keyType].Remove(handler);
            }
        }
        /// <summary>
        /// Occurs when the message dispatcher is going to dispatch a message.
        /// </summary>
        public event EventHandler<MessageDispatchEventArgs> Dispatching;
        /// <summary>
        /// Occurs when the message dispatcher failed to dispatch a message.
        /// </summary>
        public event EventHandler<MessageDispatchEventArgs> DispatchFailed;
        /// <summary>
        /// Occurs when the message dispatcher has finished dispatching the message.
        /// </summary>
        public event EventHandler<MessageDispatchEventArgs> Dispatched;

        #endregion
    }
}
