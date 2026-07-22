// -------------------------------------------------------------------------------------------------
// GameUiKit
// File: UiKit/Scripts/Framework/SceneBinding/BindAttribute.cs
// Purpose: Declarative field binding for Godot scene trees.
//
// Example
//   using GameUiKit.SceneBinding;
//
//   [Bind("Panel/VBox/BtnPlay")]
//   private Button _btnPlay = null!;
//
//   public override void _Ready()
//   {
//       SceneAutoBinder.Apply(this, nameof(MyScreen));
//       _btnPlay.Pressed += ...
//   }
//
// Why
// - UI scripts tend to have a lot of repetitive EnsureBound()/GetNode boilerplate.
// - Centralizing binding rules gives better errors and less churn during UI iteration.
// -------------------------------------------------------------------------------------------------
using System;

namespace GameUiKit.SceneBinding;

/// <summary>
/// Marks a field to be populated from the scene tree using <see cref="SceneAutoBinder"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class BindAttribute : Attribute
{
  public BindAttribute(string path)
  {
    Path = path;
  }

  /// <summary>
  /// NodePath relative to the target node.
  /// </summary>
  public string Path { get; }

  /// <summary>
  /// Optional fallback path to support transitional scene structures.
  /// </summary>
  public string? Fallback { get; init; }

  /// <summary>
  /// If true, missing nodes will result in null being assigned instead of throwing.
  /// </summary>
  public bool Optional { get; init; }
}
