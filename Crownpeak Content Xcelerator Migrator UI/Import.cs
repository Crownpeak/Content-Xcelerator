using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Crownpeak.ContentXcelerator.Migrator.UI.Dialogs;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	public partial class Import : Form
	{
		private readonly Color WarningColour = Color.FromArgb(248, 254, 180);
		private readonly Color ErrorColour = Color.FromArgb(253, 188, 188);
		private readonly Color OkColour = Color.FromArgb(202, 245, 196);

		private MigrationEngineWrapper _migrationEngine;
		private bool _checkTreeInProgress;
		private IList<LogEntry> _lastLog = new LogEntry[0];
		private int _oldTabIndex = 0;
		private List<PreImportMessageGroup> _problems;
		private bool _problemsLoading = false;
		private bool _regenerateProblems = true;
		private bool _updatingTree = false;

		public Import()
		{
			InitializeComponent();
		}

		#region Public Methods / Properties

		public void SetStatus(string message)
		{
			// Cross-thread convenience method
			Action code = () => lblStatus.Text = message;
			if (lblStatus.InvokeRequired)
			{
				lblStatus.Invoke(code);
			}
			else
			{
				code.Invoke();
			}
		}

		public string ImportLocation
		{
			get => txtImport.Text;
			set
			{
				txtImport.Text = value;
				UpdateUIStatus();
			}
		}

		#endregion

		#region Private Methods / Properties

		private void UpdateUIStatus()
		{
			btnBack.Enabled = tabWizard.SelectedIndex > 0;
			btnNext.Enabled = tabWizard.SelectedIndex < tabWizard.TabPages.Count - 1 || _lastLog.Count > 0;
			btnRefresh.Enabled = !string.IsNullOrWhiteSpace(txtImport.Text);
			btnGo.Enabled = !string.IsNullOrWhiteSpace(txtTopFolder.Text);
			btnSaveLog.Enabled = _lastLog.Count > 0;

			btnNext.Text = "&Next >";
			if (tabWizard.SelectedIndex == tabWizard.TabPages.Count - 1)
			{
				btnNext.Text = "Finish";
			}
		}

		private void EnsureCmsConnection()
		{
			try
			{
				if (_migrationEngine == null)
				{
					_migrationEngine = new MigrationEngineWrapper(txtServer.Text, txtInstance.Text, txtDeveloperKey.Text, txtUsername.Text, txtPassword.Text, rbWcoYes.Checked, txtWcoUsername.Text, txtWcoPassword.Text);
				}
			}
			catch
			{
				Common.ShowError("Error connecting to CMS");
				// Return to the first tab
				tabWizard.SelectedIndex = 0;
			}
		}

		private void ResetCmsConnection()
		{
			_migrationEngine = null;
			_regenerateProblems = true;
		}

		private async void ImportData()
		{
			btnGo.Enabled = false;

			var resources = _migrationEngine.GetCmsResources(treeViewAssets);

			// Apply the problem resolutions to the resources

			var workflowFilterMaps = new Dictionary<string, int>();
			var packageMaps = new Dictionary<string, int>();
			var stateMaps = new Dictionary<string, int>();
			var accessMaps = new Dictionary<string, int>();
			_migrationEngine.GetMaps(out accessMaps, out packageMaps, out stateMaps, out workflowFilterMaps);

			foreach (var problem in _problems.Where(p => p.Resolution.StartsWith("Mapped to ")))
			{
				// For each resource with an asset to which this fix applies
				foreach (var resource in resources.Where(r => problem.AssetIds.Contains(r.AssetId)))
				{
					switch (problem.Type)
					{
						case ProblemType.Template:
							resource.TemplateId = problem.MappedToId;
							break;
						case ProblemType.Model:
							resource.ModelId = problem.MappedToId;
							break;
						case ProblemType.Workflow:
							resource.WorkflowId = problem.MappedToId;
							break;
						case ProblemType.WorkflowFilter:
							if (!workflowFilterMaps.ContainsKey(problem.Key))
								workflowFilterMaps.Add(problem.Key, problem.MappedToId.Value);
							break;
						case ProblemType.Package:
							if (!packageMaps.ContainsKey(problem.Key))
								packageMaps.Add(problem.Key, problem.MappedToId.Value);
							break;
						case ProblemType.State:
							if (!stateMaps.ContainsKey(problem.Key))
								stateMaps.Add(problem.Key, problem.MappedToId.Value);
							break;
						case ProblemType.Access:
							if (!accessMaps.ContainsKey(problem.Key))
								accessMaps.Add(problem.Key, problem.MappedToId.Value);
							break;
					}
				}
			}

			SetStatus("Importing...");
			progressBar1.Visible = true;
			progressBar1.Value = 0;
			progressBar1.Maximum = 1;

			// Check it for an int first - saves a call back to the API to look it up
			WorklistAsset asset;
			int assetId;
			var folder = txtTopFolder.Text;
			if (int.TryParse(folder, out assetId))
			{
				asset = _migrationEngine.GetAsset(assetId);
			}
			else
			{
				if (!folder.StartsWith("/")) folder = "/" + folder;
				asset = _migrationEngine.GetAsset(folder);
			}

			var task = Task.Run(() => _migrationEngine.Import(txtImport.Text, cbxImportAssets.Checked, cbxImportLibraries.Checked, cbxImportModels.Checked, cbxImportTemplates.Checked, asset.id, resources, cbxOverwrite.Checked, accessMaps, packageMaps, stateMaps, workflowFilterMaps, OnItemProcessed));
			await task;
			_lastLog = task.Result;

			progressBar1.Visible = false;
			SetStatus("");
			UpdateUIStatus();
		}

		private void OnItemProcessed(object sender, MigrationItemEventArgs e)
		{
			var message = e.LogEntry + Environment.NewLine;

			Action code = () => txtLog.AppendText(message);
			if (txtLog.InvokeRequired)
			{
				txtLog.Invoke(code);
			}
			else
			{
				code.Invoke();
			}

			code = () =>
			{
				progressBar1.Maximum = e.Total;
				progressBar1.Increment(e.Count - progressBar1.Value);
			};
			if (progressBar1.InvokeRequired)
			{
				progressBar1.Invoke(code);
			}
			else
			{
				code.Invoke();
			}
		}

		private bool VerifyReady()
		{
			return ValidateTab(0) && ValidateTab(1) && ValidateTab(2) && ValidateTab(3)
				&& MessageBox.Show("Are you sure that you wish to proceed?", "Import?", MessageBoxButtons.YesNo) == DialogResult.Yes;
		}

		private void CheckTree(TreeNode node, bool check)
		{
			node.Checked = check;
			foreach (TreeNode child in node.Nodes)
			{
				CheckTree(child, check);
			}
		}

		private bool ValidateTab(int index, bool changingTab = false)
		{
			SetStatus("Validating...");
			bool result = true;
			switch (index)
			{
				case 0:
					if (string.IsNullOrWhiteSpace(txtServer.Text))
					{
						txtServer.Focus();
						Common.ShowWarning("Please enter a server");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtInstance.Text))
					{
						txtInstance.Focus();
						Common.ShowWarning("Please enter an instance name");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtDeveloperKey.Text))
					{
						txtDeveloperKey.Focus();
						Common.ShowWarning("Please enter a developer key");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtUsername.Text))
					{
						txtUsername.Focus();
						Common.ShowWarning("Please enter a username");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtPassword.Text))
					{
						txtPassword.Focus();
						Common.ShowWarning("Please enter a password");
						result = false;
					}
					else if (rbWcoYes.Checked && string.IsNullOrWhiteSpace(txtWcoUsername.Text))
					{
						txtWcoUsername.Select();
						Common.ShowWarning("Please enter a WCO username");
						result = false;
					}
					else if (rbWcoYes.Checked && string.IsNullOrWhiteSpace(txtWcoPassword.Text))
					{
						txtWcoPassword.Select();
						Common.ShowWarning("Please enter a WCO password");
						result = false;
					}
					else if (!MigrationEngineWrapper.Authenticate(txtServer.Text, txtInstance.Text, txtDeveloperKey.Text, txtUsername.Text, txtPassword.Text, rbWcoYes.Checked, txtWcoUsername.Text, txtWcoPassword.Text))
					{
						Common.ShowWarning("Please check your credentials");
						result = false;
					}
					else
					{
						// Success, so save the connection details
						SaveConnection();
					}
					break;
				case 1:
					if (string.IsNullOrWhiteSpace(txtImport.Text))
					{
						txtImport.Focus();
						Common.ShowWarning("Please enter a filename for the import");
						result = false;
					}
					else if (!System.IO.File.Exists(txtImport.Text))
					{
						txtImport.Focus();
						Common.ShowWarning("Please choose an existing file to import");
						result = false;
					}
					else if (!cbxImportAssets.Checked && !cbxImportLibraries.Checked && !cbxImportModels.Checked && !cbxImportTemplates.Checked)
					{
						Common.ShowWarning("Please pick at least one type of item to import");
						result = false;
					}
					else if (!TreeHasCheckedNode(treeViewAssets.Nodes))
					{
						Common.ShowWarning("Please select at least one asset to import");
						result = false;
					}
					break;
				case 2:
					if (_problemsLoading || _problems == null)
					{
						Common.ShowWarning("Please wait for the problems to finish loading");
						result = false;
					}
					else if (_problems.Any(p => string.IsNullOrWhiteSpace(p.Resolution)))
					{
						Common.ShowWarning("Please resolve all problems before importing");
						result = false;
					}
					break;
				case 3:
					if (string.IsNullOrWhiteSpace(txtTopFolder.Text))
					{
						txtTopFolder.Focus();
						Common.ShowWarning("Please enter a top-level target folder");
						result = false;
					}
					else
					{
						// Finally try to load the top level folder
						var folder = _migrationEngine.GetAsset(txtTopFolder.Text);
						if (folder == null)
						{
							txtTopFolder.Focus();
							Common.ShowWarning("Please enter a valid top-level target folder");
							result = false;
						}
					}
					break;
			}
			if (!result && !changingTab) tabWizard.SelectedIndex = index;
			SetStatus("");
			return result;
		}

		private bool TreeHasCheckedNode(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				if (TreeHasCheckedNode(node)) return true;
			}
			return false;
		}

		private bool TreeHasCheckedNode(TreeNode node)
		{
			if (node.Checked) return true;
			return TreeHasCheckedNode(node.Nodes);
		}

		private void LoadSettings()
		{
			var sessions = AppSettings.GetConnections(SettingsType.ForImport);
			cboSessions.Items.Add("");
			if (sessions.Length > 0)
			{
				cboSessions.Items.AddRange(sessions.Select(s => new ComboItem(s)).ToArray());
			}

			var lastSession = AppSettings.GetLastConnection(SettingsType.ForImport);
			if (lastSession != null)
			{
				txtServer.Text = lastSession.Server;
				txtInstance.Text = lastSession.Instance;
				txtDeveloperKey.Text = lastSession.Key;
				txtUsername.Text = lastSession.Username;
				txtPassword.Text = "";
				txtPassword.Select();
				txtWcoUsername.Text = lastSession.WcoUsername;
				txtWcoPassword.Text = "";
				if (!string.IsNullOrWhiteSpace(txtWcoUsername.Text))
					rbWcoYes.Checked = true;
				else
					rbWcoNo.Checked = true;
			}
		}

		private void SaveConnection()
		{
			var cmsInstance = new CmsInstance()
			{
				Server = txtServer.Text,
				Instance = txtInstance.Text,
				Key = txtDeveloperKey.Text,
				Username = txtUsername.Text,
				Password = "",
				WcoUsername = "",
				WcoPassword = ""
			};
			if (rbWcoYes.Checked && !string.IsNullOrWhiteSpace(txtWcoUsername.Text))
				cmsInstance.WcoUsername = txtWcoUsername.Text;
			AppSettings.SaveConnection(cmsInstance, SettingsType.ForImport);
		}

		private void UntickItems(int[] ids)
		{
			foreach (TreeNode node in treeViewAssets.Nodes)
			{
				UntickItems(node, ids);
			}
		}

		private void UntickItems(TreeNode node, int[] ids)
		{
			foreach (var id in ids)
			{
				if (node.Checked && node.Tag != null && ((CmsResource)node.Tag).AssetId == id)
				{
					node.Checked = false;
				}
			}
			foreach (TreeNode child in node.Nodes)
			{
				UntickItems(child, ids);
			}
		}

		#endregion

		#region Form Event Handlers

		private async void tabWizard_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (tabWizard.SelectedIndex == 2)
			{
				if (_regenerateProblems && !_problemsLoading)
				{
					EnsureCmsConnection();
					_problemsLoading = true;

					SetStatus("Looking for problems...");

					var task = Task.Run(() => _migrationEngine.FindProblemGroups(txtImport.Text, treeViewAssets));
					await task;
					_problems = task.Result;

					dataGridViewProblems.DataSource = _problems.OrderBy(p => p.Message).ToList();
					dataGridViewProblems.EditMode = DataGridViewEditMode.EditProgrammatically;

					SetStatus("");

					_problemsLoading = false;
					_regenerateProblems = false;
				}
			}

			UpdateUIStatus();
		}

		private void btnNext_Click(object sender, EventArgs e)
		{
			if (btnNext.Text == "Finish") Close();
			else tabWizard.SelectedIndex++;
		}

		private void btnBack_Click(object sender, EventArgs e)
		{
			tabWizard.SelectedIndex--;
		}

		private void btnVerify_Click(object sender, EventArgs e)
		{
			if (MigrationEngineWrapper.Authenticate(txtServer.Text, txtInstance.Text, txtDeveloperKey.Text, txtUsername.Text, txtPassword.Text, rbWcoYes.Checked, txtWcoUsername.Text, txtWcoPassword.Text))
			{
				Common.ShowInformation("Details verified successfully", "Success");
				SaveConnection();
			}
			else
				Common.ShowWarning("There was an error verifying your details", "Error");
		}

		private void txtImport_TextChanged(object sender, EventArgs e)
		{
			UpdateUIStatus();
		}

		private void btnImport_Click(object sender, EventArgs e)
		{
			var popup = new OpenFileDialog()
			{
				AddExtension = true,
				AutoUpgradeEnabled = true,
				CheckFileExists = true,
				DefaultExt = "xml",
				FileName = txtImport.Text,
				Filter = "Xml files (*.xml)|*.xml|All files (*.*)|*.*",
				Title = "Choose Import Location",
				ValidateNames = true
			};
			if (popup.ShowDialog() == DialogResult.OK)
			{
				txtImport.Text = popup.FileName;
			}
			UpdateUIStatus();
		}

		private void txtTopFolder_TextChanged(object sender, EventArgs e)
		{
			UpdateUIStatus();
		}

		private void btnGo_Click(object sender, EventArgs e)
		{
			if (VerifyReady())
			{
				// TODO: Are you sure?
				ImportData();
			}
		}

		private void btnRefresh_Click(object sender, EventArgs e)
		{
			var migrationWrapper = new MigrationEngineWrapper(txtServer.Text, txtInstance.Text, txtDeveloperKey.Text, txtUsername.Text, txtPassword.Text, rbWcoYes.Checked, txtWcoUsername.Text, txtWcoPassword.Text);

			treeViewAssets.Nodes.Clear();
			try
			{
				treeViewAssets.Nodes.AddRange(migrationWrapper.GetTreeNodes(txtImport.Text, cbxImportAssets.Checked, cbxImportLibraries.Checked, cbxImportModels.Checked, cbxImportTemplates.Checked));
			}
			catch (Exception ex)
			{
				Common.ShowError(ex.Message);
			}
		}

		private void treeViewAssets_AfterCheck(object sender, TreeViewEventArgs e)
		{
			if (_checkTreeInProgress) return;

			_checkTreeInProgress = true;
			CheckTree(e.Node, e.Node.Checked);
			_checkTreeInProgress = false;

			// Flag that the collection is dirty and that we need to regenerate
			if (!_updatingTree) _regenerateProblems = true;
		}

		private void btnSaveLog_Click(object sender, EventArgs e)
		{
			var popup = new SaveFileDialog()
			{
				AddExtension = true,
				AutoUpgradeEnabled = true,
				DefaultExt = "txt",
				Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
				OverwritePrompt = true,
				Title = "Save Log File",
				ValidateNames = true
			};
			if (popup.ShowDialog() == DialogResult.OK)
			{
				System.IO.File.WriteAllText(popup.FileName, MigrationEngineWrapper.GetLogAsText(_lastLog));
			}
		}

		private void tabWizard_Deselecting(object sender, TabControlCancelEventArgs e)
		{
			_oldTabIndex = e.TabPageIndex;
		}

		private void tabWizard_Selecting(object sender, TabControlCancelEventArgs e)
		{
			// Validate if they're going forwards
			if (e.TabPageIndex > _oldTabIndex)
				e.Cancel = !ValidateTab(_oldTabIndex, true);
		}

		private void Import_Load(object sender, EventArgs e)
		{
			LoadSettings();
		}

		private void cboSessions_SelectedIndexChanged(object sender, EventArgs e)
		{
			var comboItem = cboSessions.SelectedItem as ComboItem;
			if (comboItem != null)
			{
				var item = comboItem.CmsInstance;
				txtServer.Text = item.Server;
				txtInstance.Text = item.Instance;
				txtDeveloperKey.Text = item.Key;
				txtUsername.Text = item.Username;
				txtPassword.Text = "";
				txtWcoUsername.Text = item.WcoUsername;
				txtWcoPassword.Text = "";
			}
		}

		private void btnClearSessions_Click(object sender, EventArgs e)
		{
			AppSettings.ClearConnections(SettingsType.ForImport);
			cboSessions.Items.Clear();
			cboSessions.Items.Add("");
		}

		private void CmsTabText_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				if (!string.IsNullOrWhiteSpace(txtServer.Text)
						&& !string.IsNullOrWhiteSpace(txtInstance.Text)
						&& !string.IsNullOrWhiteSpace(txtDeveloperKey.Text)
						&& !string.IsNullOrWhiteSpace(txtUsername.Text)
						&& !string.IsNullOrWhiteSpace(txtPassword.Text)
						&& (rbWcoNo.Checked
						    || (!string.IsNullOrWhiteSpace(txtWcoUsername.Text)
						        && !string.IsNullOrWhiteSpace(txtWcoPassword.Text))))
				{
					tabWizard.SelectedIndex++;
				}
				else
				{
					SendKeys.Send("{TAB}");
				}
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
			else
			{
				ResetCmsConnection();
			}
		}

		private void txtImport_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return && !string.IsNullOrWhiteSpace(txtImport.Text))
			{
				btnRefresh_Click(btnRefresh, new EventArgs());
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}

		private void txtTopFolder_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return && !string.IsNullOrWhiteSpace(txtTopFolder.Text))
			{
				btnGo_Click(btnGo, new EventArgs());
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}

		private void dataGridViewProblems_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
		{
			var grid = (DataGridView)sender;

			foreach (DataGridViewRow row in grid.Rows)
			{
				var status = row.Cells["Status"].Value.ToString();
				switch (status)
				{
					case "Warning":
						row.DefaultCellStyle.BackColor = WarningColour;
						break;
					case "Error":
						row.DefaultCellStyle.BackColor = WarningColour;
						break;
				}
			}

			if (grid.Columns.Count < 5 && grid.Columns.Count > 0)
			{
				var mapColumn = new DataGridViewButtonColumn()
				{
					DisplayIndex = 2,
					Text = "Map",
					UseColumnTextForButtonValue = true,
					Width = 40,
				};
				grid.Columns.Add(mapColumn);
				var defaultColumn = new DataGridViewButtonColumn()
				{
					DisplayIndex = 3,
					Text = "Default",
					UseColumnTextForButtonValue = true,
					Width = 60,
				};
				grid.Columns.Add(defaultColumn);
				var excludeColumn = new DataGridViewButtonColumn()
				{
					DisplayIndex = 4,
					Text = "Exclude",
					UseColumnTextForButtonValue = true,
					Width = 60,
				};
				grid.Columns.Add(excludeColumn);
			}

			// Set up the widths of the button columns
			foreach (DataGridViewColumn column in grid.Columns)
			{
				if (column is DataGridViewButtonColumn)
				{
					column.MinimumWidth = 60;
					column.Width = 60;
				}
			}
		}

		private void dataGridViewProblems_CellContentClick(object sender, DataGridViewCellEventArgs e)
		{
			var grid = (DataGridView)sender;

			if (grid.Columns[e.ColumnIndex] is DataGridViewButtonColumn && e.RowIndex >= 0)
			{
				var row = grid.Rows[e.RowIndex];
				var problemGroup = (PreImportMessageGroup)row.DataBoundItem;
				if (e.ColumnIndex == 0)
				{
					// Map
					if (problemGroup.Type == ProblemType.Workflow)
					{
						var picker = new WorkflowPicker(_migrationEngine.GetWorkflows(), _migrationEngine);
						if (picker.ShowDialog() == DialogResult.OK)
						{
							problemGroup.Resolution = "Mapped to " + picker.SelectedWorkflow.Name;
							problemGroup.MappedToId = picker.SelectedWorkflow.Id;
						}
					}
					else if (problemGroup.Type == ProblemType.WorkflowFilter)
					{
						var picker = new WorkflowFilterPicker(_migrationEngine.GetWorkflowFilters());
						if (picker.ShowDialog() == DialogResult.OK)
						{
							problemGroup.Resolution = "Mapped to " + picker.SelectedWorkflowFilter.Name;
							problemGroup.MappedToId = picker.SelectedWorkflowFilter.Id;
						}
					}
					else if (problemGroup.Type == ProblemType.Package)
					{
						var picker = new PackagePicker(_migrationEngine.GetPublishingPackages());
						if (picker.ShowDialog() == DialogResult.OK)
						{
							problemGroup.Resolution = "Mapped to " + picker.SelectedPublishingPackage.Name;
							problemGroup.MappedToId = picker.SelectedPublishingPackage.Id;
						}
					}
					else
					{
						var picker = new AssetPicker(_migrationEngine);
						if (picker.ShowDialog() == DialogResult.OK)
						{
							var asset = picker.SelectedAsset;
							if (asset != null)
							{
								problemGroup.Resolution = "Mapped to " + asset.label;
								problemGroup.MappedToId = asset.id;
							}
						}
					}
				}
				if (e.ColumnIndex == 1)
				{
					// Default
					if (problemGroup.Type == ProblemType.Workflow)
					{
						problemGroup.Resolution = "Mapped to default";
						problemGroup.MappedToId = 0;
					}
					if (problemGroup.Type == ProblemType.WorkflowFilter)
					{
						problemGroup.Resolution = "Mapped to none";
						problemGroup.MappedToId = 0;
					}
					if (problemGroup.Type == ProblemType.Package)
					{
						problemGroup.Resolution = "Mapped to default";
						problemGroup.MappedToId = 1;
					}
					else if (problemGroup.Type == ProblemType.Template)
					{
						problemGroup.Resolution = "Mapped to DeveloperCS";
						problemGroup.MappedToId = 0;
					}
					else if (problemGroup.Type == ProblemType.Model)
					{
						problemGroup.Resolution = "Mapped to none";
						problemGroup.MappedToId = 0;
					}
				}
				else if (e.ColumnIndex == 2)
				{
					// Exclude
					problemGroup.Resolution = "Excluded";
					problemGroup.MappedToId = null;

					_updatingTree = true;
					UntickItems(problemGroup.AssetIds);
					_updatingTree = false;
				}
				if (string.IsNullOrWhiteSpace(problemGroup.Resolution))
				{
					row.DefaultCellStyle.BackColor = problemGroup.Status == MessageStatus.Error 
						? ErrorColour 
						: WarningColour;
				}
				else
				{
					row.DefaultCellStyle.BackColor = OkColour;
				}
				grid.Refresh();
			}
		}

		private void dataGridViewProblems_Resize(object sender, EventArgs e)
		{
			var grid = (DataGridView)sender;

			foreach (DataGridViewColumn column in grid.Columns)
			{
				if (column is DataGridViewButtonColumn)
					column.Width = 60;
			}
		}

		private void RbWcoYes_CheckedChanged(object sender, EventArgs e)
		{
			EnableWcoControls(rbWcoYes.Checked);
		}

		private void RbWcoNo_CheckedChanged(object sender, EventArgs e)
		{
			EnableWcoControls(rbWcoYes.Checked);
		}

		private void EnableWcoControls(bool enable)
		{
			lblWcoUsername.Enabled = lblWcoPassword.Enabled = txtWcoUsername.Enabled = txtWcoPassword.Enabled = enable;
		}
		#endregion
	}
}
