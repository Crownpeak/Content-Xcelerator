using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI.Dialogs
{
	public partial class PackagePicker : Form
	{
		private Dictionary<int, PublishingPackage> _packages;

		public PublishingPackage SelectedPublishingPackage { get; set; }

		public PackagePicker(Dictionary<int, PublishingPackage> packages)
		{
			_packages = packages;
			SelectedPublishingPackage = null;

			InitializeComponent();
		}

		private void PackagePicker_Load(object sender, EventArgs e)
		{
			comboBox1.ValueMember = "Key";
			comboBox1.DisplayMember = "Value";

			comboBox1.DataSource = _packages.Select(p => new KeyValuePair<int, string>(p.Key, p.Value.Name)).ToList();
		}

		private void btnOK_Click(object sender, EventArgs e)
		{
			SelectedPublishingPackage = _packages[(int)comboBox1.SelectedValue];
			DialogResult = DialogResult.OK;
			Close();
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			SelectedPublishingPackage = null;
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
