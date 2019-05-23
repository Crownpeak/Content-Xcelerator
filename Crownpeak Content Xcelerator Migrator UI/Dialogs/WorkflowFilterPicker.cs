using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI.Dialogs
{
	public partial class WorkflowFilterPicker : Form
	{
		private Dictionary<int, WorkflowFilter> _workflowFilters;

		public WorkflowFilter SelectedWorkflowFilter { get; set; }

		public WorkflowFilterPicker(Dictionary<int, WorkflowFilter> workflowFilters)
		{
			_workflowFilters = workflowFilters;
			SelectedWorkflowFilter = null;

			InitializeComponent();
		}

		private void WorkflowFilterPicker_Load(object sender, EventArgs e)
		{
			comboBox1.ValueMember = "Key";
			comboBox1.DisplayMember = "Value";

			comboBox1.DataSource = _workflowFilters.Select(w => new KeyValuePair<int, string>(w.Key, w.Value.Name)).ToList();
		}

		private void btnOK_Click(object sender, EventArgs e)
		{
			SelectedWorkflowFilter = _workflowFilters[(int)comboBox1.SelectedValue];
			DialogResult = DialogResult.OK;
			Close();
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			SelectedWorkflowFilter = null;
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
