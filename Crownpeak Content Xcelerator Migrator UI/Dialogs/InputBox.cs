using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Crownpeak.ContentXcelerator.Migrator.UI.Dialogs
{
	public partial class InputBox : Form
	{
		private const int HSPACING = 4;
		private const int VSPACING = 12;

		// See http://www.codeproject.com/Articles/9656/Dissecting-the-MessageBox#DisableClose
		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

		private const int SC_CLOSE = 0xF060;
		private const int MF_BYCOMMAND = 0x0;
		private const int MF_GRAYED = 0x1;
		private const int MF_ENABLED = 0x0;

		public InputBox()
		{
			InitializeComponent();
			label1.Font = System.Drawing.SystemFonts.MessageBoxFont;
		}

		public string Label
		{
			get => label1.Text;
			set => label1.Text = value;
		}

		public string Value
		{
			get => textBox1.Text;
			set => textBox1.Text = value;
		}

		#region Static Methods

		public static DialogResult Show(string message, out string result, string defaultValue = "")
		{
			return Show(message, "Message", MessageBoxButtons.OK, out result, defaultValue);
		}

		public static DialogResult Show(string message, string caption, out string result, string defaultValue = "")
		{
			return Show(message, caption, MessageBoxButtons.OK, out result, defaultValue);
		}

		public static DialogResult Show(string message, string caption, MessageBoxButtons buttons, out string result, string defaultValue = "")
		{
			var frm = new InputBox();
			frm.Text = caption;
			// TODO: this will probably look awful for a big message
			frm.Label = message;
			frm.Value = defaultValue ?? "";
			EnableMenuItem(GetSystemMenu(frm.Handle, false), SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);

			var myButtons = new List<Button>();
			switch (buttons)
			{
				case MessageBoxButtons.AbortRetryIgnore:
					myButtons.Add(frm.MakeButton("&Abort", DialogResult.Abort, false, false));
					myButtons.Add(frm.MakeButton("&Retry", DialogResult.Retry, false, false));
					myButtons.Add(frm.MakeButton("&Ignore", DialogResult.Ignore, false, false));
					break;
				case MessageBoxButtons.OK:
					myButtons.Add(frm.MakeButton("&Ok", DialogResult.OK, true, true));
					break;
				case MessageBoxButtons.OKCancel:
					myButtons.Add(frm.MakeButton("&Ok", DialogResult.OK, true, false));
					myButtons.Add(frm.MakeButton("&Cancel", DialogResult.Cancel, false, true));
					break;
				case MessageBoxButtons.RetryCancel:
					myButtons.Add(frm.MakeButton("&Retry", DialogResult.Retry, false, false));
					myButtons.Add(frm.MakeButton("&Cancel", DialogResult.Cancel, false, true));
					break;
				case MessageBoxButtons.YesNo:
					myButtons.Add(frm.MakeButton("&Yes", DialogResult.Yes, true, false));
					myButtons.Add(frm.MakeButton("&No", DialogResult.No, false, true));
					break;
				case MessageBoxButtons.YesNoCancel:
					myButtons.Add(frm.MakeButton("&Yes", DialogResult.Yes, true, false));
					myButtons.Add(frm.MakeButton("&No", DialogResult.No, false, false));
					myButtons.Add(frm.MakeButton("&Cancel", DialogResult.Cancel, false, true));
					break;
			}

			var offset = frm.DesktopBounds.Size - frm.ClientSize;
			frm.Width = offset.Width + frm.label1.Width;
			frm.Height = offset.Height + myButtons[0].Top + myButtons[0].Height + VSPACING;

			frm.PositionButtons(myButtons);

			var dialogResult = frm.ShowDialog();
			result = frm.Value;
			return dialogResult;
		}

		#endregion

		private Button MakeButton(string text, DialogResult result, bool isOk, bool isCancel)
		{
			var button = new Button
			{
				Text = text,
				DialogResult = result,
				Height = 24,
				Width = 86,
				Top = label1.Height + textBox1.Height + VSPACING * 2,
				Font = System.Drawing.SystemFonts.MessageBoxFont
			};
			button.Click += Button_Click;
			if (isOk) AcceptButton = button;
			if (isCancel) CancelButton = button;
			Controls.Add(button);
			if (isOk) button.Focus();

			return button;
		}

		private void PositionButtons(List<Button> buttons)
		{
			if (buttons.Count == 0) return;

			var buttonWidth = buttons[0].Width;

			var left = Width - buttonWidth - HSPACING * 6;
			for (var i = buttons.Count - 1; i >= 0; i--)
			{
				var button = buttons[i];
				button.Left = left;
				button.Anchor = AnchorStyles.Right & AnchorStyles.Bottom;
				left -= buttonWidth + HSPACING * 2;
			}
		}

		private void Button_Click(object sender, EventArgs e)
		{
			if (sender is Button)
			{
				DialogResult = (sender as Button).DialogResult;
				Close();
			}
		}
	}
}
