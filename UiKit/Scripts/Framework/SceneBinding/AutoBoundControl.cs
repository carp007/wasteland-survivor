// -------------------------------------------------------------------------------------------------
// GameUiKit
// File: UiKit/Scripts/Framework/SceneBinding/AutoBoundControl.cs
// Purpose: Optional base Control that automatically applies SceneAutoBinder in _Ready().
// -------------------------------------------------------------------------------------------------
using Godot;

namespace GameUiKit.SceneBinding;

/// <summary>
/// Optional convenience base class that calls <see cref="SceneAutoBinder.Apply"/> during <see cref="_Ready"/>.
/// </summary>
/// <remarks>
/// Use this for UI screens/controls that mainly consist of bound child nodes + UI logic.
/// If you override <see cref="_Ready"/>, call <c>base._Ready()</c> or call <see cref="SceneAutoBinder.Apply"/>
/// manually.
/// </remarks>
public abstract partial class AutoBoundControl : Control
{
    /// <summary>
    /// Binder context shown in error messages. Defaults to the runtime type name.
    /// </summary>
    protected virtual string BindContextName => GetType().Name;

    public override void _Ready()
    {
        base._Ready();
        SceneAutoBinder.Apply(this, BindContextName);
        AfterBind();
    }

    /// <summary>
    /// Called after binding has been applied successfully. Override to attach events.
    /// </summary>
    protected virtual void AfterBind() { }
}
