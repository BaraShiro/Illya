/*
    File:       RegistryReader.cs
    Version:    1.0.0
    Author:     Robert Rosborg
 
 */

#nullable enable
using System;
using Microsoft.Win32;

namespace Illya
{
      
    /// <summary>
    /// The exception that is thrown when an error occured while accessing the registry.
    /// </summary>
    internal class RegistryErrorException : Exception
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="RegistryErrorException"/> class with an error message and
        /// a reference to the exception that caused this exception.
        /// </summary>
        /// <param name="message">The error message explaining why this exception was thrown.</param>
        /// <param name="e">The inner exception that triggered the trowing of this exception.</param>
        public RegistryErrorException(string message, Exception e) : base(message, e){}
        
        /// <summary>
        /// Initialises a new instance of the <see cref="RegistryErrorException"/> class with an error message. 
        /// </summary>
        /// <param name="message">The error message explaining why this exception was thrown.</param>
        public RegistryErrorException(string message) : base(message){}
    }
    
    /// <summary>
    /// A class containing methods for storing and retrieving values from the registry.
    /// </summary>
    public static class RegistryReader
    {
        /// <summary>
        /// Retrieves an int value from the registry.
        /// </summary>
        /// <param name="key">The registry key that contains the name/value pair.</param>
        /// <param name="name">The name of the int value to retrieve.</param>
        /// <param name="fallback">A fallback value that is returned if a valid value
        /// is not found in the registry.</param>
        /// <returns>The int value associated with <paramref name="name"/>,
        /// or <paramref name="fallback"/> if no such value is found.</returns>
        public static int ReadIntFromRegistry(RegistryKey key, string name, int fallback)
        {
            object? value = ReadValueFromRegistry(key, name);
            
            return (int) (value ?? fallback);
        }
        
        /// <summary>
        /// Retrieves a double value from the registry.
        /// </summary>
        /// <param name="key">The registry key that contains the name/value pair.</param>
        /// <param name="name">The name of the double value to retrieve.</param>
        /// <param name="fallback">A fallback value that is returned if a valid value
        /// is not found in the registry.</param>
        /// <returns>The double value associated with <paramref name="name"/>,
        /// or <paramref name="fallback"/> if no such value is found or the found value is invalid.</returns>
        public static double ReadDoubleFromRegistry(RegistryKey key, string name, double fallback)
        {
            string? value = ReadValueFromRegistry(key, name)?.ToString();
        
            if (value == null) return fallback;
                    
            try
            {
                return double.Parse(value);
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                return fallback;
            }
        }
        
        /// <summary>
        /// Retrieves a bool value from the registry.
        /// </summary>
        /// <param name="key">The registry key that contains the name/value pair.</param>
        /// <param name="name">The name of the bool value to retrieve.</param>
        /// <param name="fallback">A fallback value that is returned if a valid value
        /// is not found in the registry.</param>
        /// <returns>The bool value associated with <paramref name="name"/>,
        /// or <paramref name="fallback"/> if no such value is found or the found value is invalid.</returns>
        public static bool ReadBoolFromRegistry(RegistryKey key, string name, bool fallback)
        {
            string? value = ReadValueFromRegistry(key, name)?.ToString();
        
            if (value != null && (value == bool.TrueString || value == bool.FalseString))
            {
                return bool.Parse(value);
            }
        
            return fallback;
        }
        
        /// <summary>
        /// Retrieves a value from the registry.
        /// </summary>
        /// <param name="key">The registry key that contains the name/value pair.</param>
        /// <param name="name">The name of the value to retrieve.</param>
        /// <returns>The value associated with <paramref name="name"/>,
        /// or null if no such value is found or the retrieval failed.</returns>
        private static object? ReadValueFromRegistry(RegistryKey key, string name)
        {
            try
            {
                return key.GetValue(name);
            }
            catch (Exception e) when 
            (e is System.Security.SecurityException 
                or ObjectDisposedException 
                or System.IO.IOException 
                or UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Stores a value in the registry.
        /// </summary>
        /// <param name="key">The registry key that contains the name/value pair.</param>
        /// <param name="name">The name of the value to store.</param>
        /// <param name="value">The value to store.</param>
        /// <exception cref="RegistryErrorException">Unable to write data to <paramref name="key"/>.</exception>
        public static void WriteValueToRegistry(RegistryKey key, string name, object value)
        {
            try
            {
                key.SetValue(name, value);
            }
            catch (Exception e) when
            (e is System.Security.SecurityException
                or ObjectDisposedException
                or System.IO.IOException
                or UnauthorizedAccessException)
            {
                throw new RegistryErrorException("An exception was thrown while accessing the registry.", e);
            }
            catch (ArgumentException)
            {
                // Tried to write an unsupported data type to registry, ignored
            }
        }
    }
}