using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Provides centralized access to game systems and services using the Service Locator pattern.
    /// This is a core system that initializes before other MonoBehaviours.
    /// </summary>
    [DefaultExecutionOrder(-9999)]
    public class GameSystemLocator : MonoBehaviour
    {
        private static GameSystemLocator s_Instance;
        private readonly Dictionary<Type, object> m_Services = new();
        
        public static bool IsInitialized { get; private set; }
        
        private void Awake()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (s_Instance != this)
            {
                Logger.LogError("Service Locator Singleton already exists!");
                Destroy(gameObject);
            }
        }

        public static GameSystemLocator Instance => s_Instance ?? 
            throw new InvalidOperationException("GameSystemLocator is not initialized.");

        // Registration method
        private static void Register(Type type, object service)
        {
            if (!s_Instance.m_Services.TryAdd(type, service))
            {
                throw new InvalidOperationException($"Service of type {type.Name} is already registered.");
            }

        }

        /// <summary>
        /// Registers a service of type T. Services must be registered before they can be accessed.
        /// Throws if a service of the same type is already registered.
        /// </summary>
        /// <typeparam name="T">The type of service to register</typeparam>
        /// <param name="service">The service instance</param>
        /// <exception cref="InvalidOperationException">Thrown if service type is already registered</exception>
        public static void Register<T>(T service) where T : class
        {
            Register(typeof(T), service);
        }

        /// <summary>
        /// Retrieves a registered service of type T.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve</typeparam>
        /// <returns>The registered service instance</returns>
        /// <exception cref="InvalidOperationException">Thrown if service is not registered</exception>
        public static T Get<T>() where T : class
        {
            if (s_Instance == null)
            {
                throw new InvalidOperationException(
                    "GameSystemLocator is not initialized. This usually means you started from the wrong scene. " +
                    "Please start from the InitGemHunterUGS to ensure proper game initialization."
                );
            }
            if (s_Instance.m_Services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            throw new InvalidOperationException($"Service of type {typeof(T)} not registered.");
        }
        
        private static bool IsServiceRegistered(Type type)
        {
            return s_Instance.m_Services.ContainsKey(type);
        }
        
        /// <summary>
        /// Allows a service to unregister itself from the locator.
        /// Services can only unregister themselves as a security measure.
        /// </summary>
        /// <typeparam name="T">The type of service being unregistered</typeparam>
        /// <param name="service">The service instance to unregister</param>
        public static void UnregisterSelf<T>(T service) where T : class
        {
            Type type = typeof(T);
            if (s_Instance.m_Services.TryGetValue(type, out var registeredService))
            {
                if (ReferenceEquals(service, registeredService))
                {
                    s_Instance.m_Services.Remove(type);
                    if (service is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    // Logger.Log($"Service of type {type.Name} has unregistered itself.");
                }
                else
                {
                    Logger.LogWarning($"Attempted unauthorized unregistration of service type {type.Name}.");
                }
            }
            else
            {
                Logger.LogWarning($"Attempted to unregister non-existent service of type {type.Name}.");
            }
        }
        
        private void OnApplicationQuit()
        {
            foreach (var service in m_Services.Values)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
