using System;
using System.Windows.Forms;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	public partial class Startup : Form
	{
		public Startup()
		{
			InitializeComponent();
		}

		private void btnBoth_Click(object sender, EventArgs e)
		{
			var frm = new Export();
			this.Hide();
			frm.Show();
			frm.Closed += (s, args) =>
			{
				var pos = frm.Location;
				var size = frm.Size;
				var file = frm.ExportLocation;

				var frm2 = new Import();
				frm2.ImportLocation = file;
				frm2.Show();
				frm2.Location = pos;
				frm2.Size = size;
				frm2.Closed += (s2, args2) => this.Show();
			};
		}

		private void btnExport_Click(object sender, EventArgs e)
		{
			var frm = new Export();
			this.Hide();
			frm.Show();
			frm.Closed += (s, args) => this.Show();
		}

		private void btnImport_Click(object sender, EventArgs e)
		{
			var frm = new Import();
			this.Hide();
			frm.Show();
			frm.Closed += (s, args) => this.Show();
		}
	}
}
