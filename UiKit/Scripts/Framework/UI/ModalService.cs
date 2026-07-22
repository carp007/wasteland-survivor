// -------------------------------------------------------------------------------------------------
// UiKit
// File: Scripts/Framework/UI/ModalService.cs
// Purpose: Default IModalService implementation backed by ModalHost. Includes small, reusable
//          message/confirm dialog builders (code-only, no scenes required).
// -------------------------------------------------------------------------------------------------
using System;
using Godot;

namespace GameUiKit.UI;

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

		var row = BuildCenteredButtonRow();
		var btn = BuildDialogButton(closeText);
		btn.Pressed += () =>
		{
			handle?.Close();
			onClosed?.Invoke();
		};
		row.AddChild(btn);
		dialog.AddButtons(row);

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

		var row = BuildCenteredButtonRow();

		var btnConfirm = BuildDialogButton(confirmText);
		btnConfirm.Pressed += () =>
		{
			handle?.Close();
			onConfirm();
		};
		row.AddChild(btnConfirm);

		var btnCancel = BuildDialogButton(cancelText);
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

	private static HBoxContainer BuildCenteredButtonRow()
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		row.AddThemeConstantOverride("separation", 12);
		return row;
	}

	private static Button BuildDialogButton(string text)
	{
		return new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(156, 42),
			Alignment = HorizontalAlignment.Center,
			FocusMode = Control.FocusModeEnum.All,
		};
	}
}
