// -------------------------------------------------------------------------------------------------
// Wasteland Survivor
// File: Scripts/Framework/UI/ModalService.cs
// Purpose: Default IModalService implementation backed by ModalHost. Includes small, reusable
//          message/confirm dialog builders (code-only, no scenes required).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace WastelandSurvivor.Framework.UI;

public sealed class ModalService : IModalService
{
	private readonly ModalHost _host;
	private readonly ModalDialogStyle _style;

	public ModalService(ModalHost host, ModalDialogStyle? style = null)
	{
		_host = host ?? throw new ArgumentNullException(nameof(host));
		_style = style ?? new ModalDialogStyle();
	}

	public IModalHandle Show(Control content, ModalOptions? options = null)
		=> _host.Show(content, options ?? new ModalOptions());

	public IModalHandle ShowMessage(
		string title,
		string body,
		string closeText = "Close",
		Action? onClosed = null,
		ModalOptions? options = null)
	{
		IModalHandle? handle = null;
		var dialog = BuildDialog(title, body);

		var btn = new Button { Text = closeText };
		btn.Pressed += () =>
		{
			handle?.Close();
			onClosed?.Invoke();
		};
		dialog.AddButtons(btn);

		handle = Show(dialog, options ?? new ModalOptions(DimBackground: true, CloseOnEscape: true, AutoFocus: true));
		return handle;
	}

	public IModalHandle ShowConfirm(
		string title,
		string body,
		string confirmText,
		string cancelText,
		Action onConfirm,
		Action? onCancel = null,
		ModalOptions? options = null)
	{
		if (onConfirm == null) throw new ArgumentNullException(nameof(onConfirm));

		IModalHandle? handle = null;
		var dialog = BuildDialog(title, body);

		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		row.AddThemeConstantOverride("separation", 10);

		var btnConfirm = new Button { Text = confirmText };
		btnConfirm.Pressed += () =>
		{
			handle?.Close();
			onConfirm();
		};
		row.AddChild(btnConfirm);

		var btnCancel = new Button { Text = cancelText };
		btnCancel.Pressed += () =>
		{
			handle?.Close();
			onCancel?.Invoke();
		};
		row.AddChild(btnCancel);

		dialog.AddButtons(row);

		handle = Show(dialog, options ?? new ModalOptions(DimBackground: true, CloseOnEscape: true, AutoFocus: true));
		return handle;
	}

	private DialogCard BuildDialog(string title, string body)
	{
		var dialog = new DialogCard
		{
			ThemeApplier = _style.ThemeApplier,
			DialogStyler = _style.DialogStyler,
		};

		dialog.Configure(title, body, minSize: _style.DefaultMinSize ?? new Vector2(440, 220));
		return dialog;
	}
}
