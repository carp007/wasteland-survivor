// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/App/GameServices.cs
// Purpose: Tiny service registry (singleton DI). Used to avoid threading service references throughout the node tree.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

using WastelandSurvivor.Core.IO;

namespace WastelandSurvivor.Game;

public sealed class GameServices
{
	private readonly Dictionary<Type, object> _singletons = new();

	public void AddSingleton<T>(T instance) where T : class
		=> _singletons[typeof(T)] = instance ?? throw new ArgumentNullException(nameof(instance));

	public T Get<T>() where T : class
	{
		if (_singletons.TryGetValue(typeof(T), out var obj))
			return (T)obj;

		throw new InvalidOperationException($"Service not registered: {typeof(T).FullName}");
	}

	public bool TryGet<T>(out T? value) where T : class
	{
		if (_singletons.TryGetValue(typeof(T), out var obj))
		{
			value = (T)obj;
			return true;
		}

		value = null;
		return false;
	}

	/// <summary>
	/// Convenience accessor for the shared definition database.
	/// </summary>
	public DefDatabase Defs => Get<DefDatabase>();
}
