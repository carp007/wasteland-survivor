// -------------------------------------------------------------------------------------------------
// GameUiKit
// File: UiKit/Scripts/Framework/SceneBinding/AutoBoundNode.cs
// Purpose: Optional base Node that automatically applies SceneAutoBinder in _Ready().
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GameUiKit.SceneBinding;

/// <summary>
/// Optional convenience base class that calls <see cref="SceneAutoBinder.Apply"/> during <see cref="_Ready"/>.
/// </summary>
/// <remarks>
/// Useful for non-UI scripts that still want attribute-based binding to child nodes.
/// If you override <see cref="_Ready"/>, call <c>base._Ready()</c> or call <see cref="SceneAutoBinder.Apply"/>
/// manually.
/// </remarks>
public abstract partial class AutoBoundNode : Node
{
    protected virtual string BindContextName => GetType().Name;

    public override void _Ready()
    {
        base._Ready();
        SceneAutoBinder.Apply(this, BindContextName);
        AfterBind();
    }

    protected virtual void AfterBind() { }
}
