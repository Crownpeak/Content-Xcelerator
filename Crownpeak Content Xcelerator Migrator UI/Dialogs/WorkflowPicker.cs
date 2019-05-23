using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI.Dialogs
{
	public partial class WorkflowPicker : Form
	{
		private Dictionary<int, WorkflowData> _workflows;
		private MigrationEngineWrapper _migrationEngine;

		public WorkflowData SelectedWorkflow { get; set; }

		public WorkflowPicker(Dictionary<int, WorkflowData> workflows, MigrationEngineWrapper migrationEngine)
		{
			_workflows = workflows;
			_migrationEngine = migrationEngine;
			SelectedWorkflow = null;

			InitializeComponent();
		}

		private void WorkflowPicker_Load(object sender, EventArgs e)
		{
			comboBox1.ValueMember = "Key";
			comboBox1.DisplayMember = "Value";

			comboBox1.DataSource = _workflows.Select(w => new KeyValuePair<int, string>(w.Key, _migrationEngine.GetAsset(w.Value.AssetId).FullPath)).ToList();
		}

		private void btnOK_Click(object sender, EventArgs e)
		{
			SelectedWorkflow = _workflows[(int)comboBox1.SelectedValue];
			DialogResult = DialogResult.OK;
			Close();
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			SelectedWorkflow = null;
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
