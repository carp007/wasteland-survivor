// -------------------------------------------------------------------------------------------------
// GameUiKit
// File: UiKit/Scripts/Framework/SceneBinding/SceneAutoBinder.cs
// Purpose: Reflection-driven binder that populates fields marked with <see cref="BindAttribute"/>.
//
// Notes
// - Intended for UI scripts (menus/HUD) where binding churn is high.
// - Uses a per-type cache so reflection cost is paid once.
// - Delegates to <see cref="SceneBinder"/> so binding errors remain consistent.
// -------------------------------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace GameUiKit.SceneBinding;

/// <summary>
/// Populates fields on a Node instance that are decorated with <see cref="BindAttribute"/>.
/// </summary>
public static class SceneAutoBinder
{
  private sealed record FieldBinding(FieldInfo Field, BindAttribute Attr);

  private static readonly ConcurrentDictionary<Type, List<FieldBinding>> Cache = new();

  /// <summary>
  /// Bind all <see cref="BindAttribute"/> fields on the given node.
  /// </summary>
  public static void Apply(object target, string? context = null)
  {
    if (target is not Node node)
      throw new ArgumentException("SceneAutoBinder.Apply target must be a Godot Node.", nameof(target));

    var bindings = Cache.GetOrAdd(target.GetType(), BuildBindings);
    if (bindings.Count == 0)
      return;

    var binder = new SceneBinder(node, context);

    foreach (var b in bindings)
    {
      var fieldType = b.Field.FieldType;
      if (!typeof(Node).IsAssignableFrom(fieldType))
        continue;
      var resolved = ResolveNode(binder, fieldType, b.Attr);
      b.Field.SetValue(target, resolved);
    }
  }

  private static List<FieldBinding> BuildBindings(Type t)
  {
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    return t
      .GetFields(flags)
      .Select(f => (Field: f, Attr: f.GetCustomAttribute<BindAttribute>(inherit: true)))
      .Where(x => x.Attr != null)
      .Select(x => new FieldBinding(x.Field, x.Attr!))
      .ToList();
  }

  private static Node? ResolveNode(SceneBinder binder, Type nodeType, BindAttribute attr)
  {
    var path = attr.Path;
    var fallback = string.IsNullOrWhiteSpace(attr.Fallback) ? null : attr.Fallback;

    // Required with fallback.
    if (!attr.Optional && fallback != null)
      return InvokeGeneric(binder, nameof(SceneBinder.ReqFallback), nodeType, path, fallback);

    // Required.
    if (!attr.Optional)
      return InvokeGeneric(binder, nameof(SceneBinder.Req), nodeType, path);

    // Optional with fallback.
    if (fallback != null)
    {
      var primary = InvokeGeneric(binder, nameof(SceneBinder.Opt), nodeType, path);
      return primary ?? InvokeGeneric(binder, nameof(SceneBinder.Opt), nodeType, fallback);
    }

    // Optional.
    return InvokeGeneric(binder, nameof(SceneBinder.Opt), nodeType, path);
  }

  private static Node? InvokeGeneric(SceneBinder binder, string methodName, Type t, params object[] args)
  {
    var mi = typeof(SceneBinder)
      .GetMethods(BindingFlags.Instance | BindingFlags.Public)
      .First(m => m.Name == methodName && m.IsGenericMethodDefinition && m.GetParameters().Length == args.Length);
    var g = mi.MakeGenericMethod(t);
    return (Node?)g.Invoke(binder, args);
  }
}
