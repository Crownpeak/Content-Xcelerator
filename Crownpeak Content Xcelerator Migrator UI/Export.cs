using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrownPeak.AccessApiHelper;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	public partial class Export : Form
	{
		private const string LOADING = "Loading...";
		private AsyncLock _loadingLock = new AsyncLock();
		private MigrationEngineWrapper _migrationEngine;
		private bool _checkTreeInProgress;
		private int _loadsInProgress = 0;
		private IList<LogEntry> _lastLog = new LogEntry[0];
		private int _oldTabIndex = 0;

		public Export()
		{
			InitializeComponent();
		}

		#region Public Methods and Properties

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

		public void AddRootNode(TreeNode node)
		{
			// Cross-thread convenience method
			Action code = () => treeViewAssets.Nodes.Add(node);
			if (treeViewAssets.InvokeRequired)
			{
				treeViewAssets.Invoke(code);
			}
			else
			{
				code.Invoke();
			}
		}

		public void SetChildNodes(TreeNode parent, TreeNode[] children, bool expand)
		{
			// Cross-thread convenience method
			Action code = () =>
			{
				parent.Nodes.Clear();
				parent.Nodes.AddRange(children);
				if (expand) parent.Expand();
			};
			if (parent.TreeView != null && parent.TreeView.InvokeRequired)
			{
				parent.TreeView.Invoke(code);
			}
			else
			{
				code.Invoke();
			}
		}

		public string ExportLocation
		{
			get { return txtExportTo.Text; }
			set
			{
				txtExportTo.Text = value;
				UpdateUIStatus();
			}
		}

		#endregion

		#region Private Methods and Properties

		private void UpdateUIStatus()
		{
			Action code = () =>
			{
				btnBack.Enabled = tabWizard.SelectedIndex > 0;
				btnNext.Enabled = tabWizard.SelectedIndex < tabWizard.TabPages.Count - 1 || (_lastLog != null && _lastLog.Count > 0);
				btnGo.Enabled = _loadsInProgress == 0 && !string.IsNullOrWhiteSpace(txtExportTo.Text);
				cbxSeparateBinaries.Enabled = true;
				btnSaveLog.Enabled = (_lastLog != null && _lastLog.Count > 0);

				btnNext.Text = "&Next >";
				if (tabWizard.SelectedIndex == tabWizard.TabPages.Count - 1)
				{
					btnNext.Text = "Finish";
				}
			};

			if (tabWizard.InvokeRequired)
			{
				tabWizard.Invoke(code);
			}
			else
			{
				code.Invoke();
			}

			if (_loadsInProgress > 0) SetStatus("Loading...");
			else SetStatus("");
		}

		private async Task<WorklistAsset> GetAssetBackground(string idOrPath)
		{
			WorklistAsset result;
			using (var releaser = await _loadingLock.LockAsync())
			{
				var task = Task.Run(() => _migrationEngine.GetAsset(idOrPath));
				result = await task;
			}
			return result;
		}

		private async void LoadChildrenBackground(TreeNode node)
		{
			_loadsInProgress++;
			UpdateUIStatus();

			if (HasLoadingNode(node))
			{
				var result = new TreeNode[0];
				using (var releaser = await _loadingLock.LockAsync())
				{
					var task = Task.Run(() => LoadChildren(node));
					result = await task;
				}
				SetChildNodes(node, result, true);
			}

			_loadsInProgress--;
			UpdateUIStatus();
		}

		private TreeNode[] LoadChildren(TreeNode node)
		{
			var result = new List<TreeNode>();
			if (!(node.Nodes.Count == 1 && node.Nodes[0].Text == LOADING)) return result.ToArray();

			EnsureCmsConnection();
			var contents = _migrationEngine.GetAssetList(((CmsResource)node.Tag).AssetId);

			foreach (var item in contents)
			{
				if (!IsExcluded(item))
				{
					// Remove a leading "/root" from the path
					if (!string.IsNullOrWhiteSpace(item.Asset.FullPath) && item.Asset.FullPath.StartsWith("/root"))
					{
						item.Asset.FullPath = item.Asset.FullPath.Substring(5);
					}

					var childNode = new TreeNode(item.Name)
					{
						ImageIndex = Common.GetImageIndex(item.AssetType),
						Tag = item
					};
					childNode.SelectedImageIndex = childNode.ImageIndex;
					if (Common.AssetTypeCanHaveChildren(item.AssetType))
					{
						childNode.Nodes.Add(LoadingNode());
					}
					result.Add(childNode);
				}
			}
			return result.ToArray();
		}

		private async void LoadDescendentsBackground(TreeNode node, bool start = false)
		{
			if (start)
			{
				_loadsInProgress++;
				UpdateUIStatus();
			}

			if (HasLoadingNode(node))
			{
				using (var releaser = await _loadingLock.LockAsync())
				{
					var task = Task.Run(() => LoadChildren(node));
					var result = await task;
					if (result.Any() || HasLoadingNode(node))
					{
						SetChildNodes(node, result, false);
					}
				}
			}

			foreach (TreeNode childNode in node.Nodes)
			{
				childNode.Checked = true;
				LoadDescendentsBackground(childNode);
			}

			if (start)
			{
				_loadsInProgress--;
				UpdateUIStatus();
			}
		}

		private TreeNode LoadingNode()
		{
			var node = new TreeNode(LOADING);
			node.Tag = null;
			return node;
		}

		private bool HasLoadingNode(TreeNode node)
		{
			return node.Nodes.Count == 1 && node.Nodes[0].Text == LOADING;
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
			treeViewAssets.Nodes.Clear();
		}

		private async void ExportData()
		{
			btnGo.Enabled = false;
			cbxSeparateBinaries.Enabled = false;

			var resources = _migrationEngine.GetCmsResources(treeViewAssets);
			
			SetStatus("Exporting...");
			progressBar1.Visible = true;
			progressBar1.Value = 0;
			progressBar1.Maximum = 1;
			var task = Task.Run(() => _migrationEngine.Export(txtExportTo.Text, ((CmsResource)treeViewAssets.Nodes[0].Tag).AssetId, cbxExportAssets.Checked, cbxExportLibraries.Checked, cbxExportModels.Checked, cbxExportTemplates.Checked, cbxExportBinaries.Checked, cbxSeparateBinaries.Checked, resources, OnItemProcessed));
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
			return ValidateTab(0) && ValidateTab(1) && ValidateTab(2);
		}

		private void RefreshFolders()
		{
			treeViewAssets.Nodes.Clear();

			EnsureCmsConnection();
			// If we can identify this as an int, we can save a call
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
			if (asset == null)
			{
				Common.ShowError("Unable to find asset with that path or id");
			}
			else
			{
				_migrationEngine.PopulateAssetFullPath(asset);
				var isSite = MigrationEngineWrapper.IsSite(asset);
				var rootId = asset.id;
				var rootNode = new TreeNode("Folder " + rootId)
				{
					ImageIndex = Common.GetImageIndex(isSite ? CmsAssetType.Site : CmsAssetType.Folder),
					Tag = new CmsResource {AssetId = asset.id, Asset = asset, Name = asset.FullPath, Path = asset.FullPath},
					Text = asset.FullPath
				};
				rootNode.SelectedImageIndex = rootNode.ImageIndex;
				rootNode.Nodes.Add(LoadingNode());
				LoadChildrenBackground(rootNode);
				AddRootNode(rootNode);
				if (!isSite && !asset.FullPath.Equals("/") && !asset.FullPath.Equals("/System", StringComparison.OrdinalIgnoreCase))
				{
					// This is not a site, so we need to load the appropriate system folders
					if (cbxExportLibraries.Checked)
					{
						// TODO: this should be library folder when available
						AddSystemFolder("/System/Library", "/System/Library", CmsAssetType.LibraryFolder);
					}
					if (cbxExportModels.Checked)
					{
						AddSystemFolder("/System/Models", "/System/Models");
					}
					if (cbxExportTemplates.Checked)
					{
						AddSystemFolder("/System/Templates", "/System/Templates");
					}
				}
			}
		}

		private void AddSystemFolder(string path, string label, CmsAssetType assetType = CmsAssetType.Folder)
		{
			var task = GetAssetBackground(path);
			task.ContinueWith((antecedent) =>
			{
				var asset = antecedent.Result;
				asset.FullPath = path;
				var node = new TreeNode(label)
				{
					Tag = new CmsResource {AssetId = asset.id, Asset = asset, Name = label, Path = path},
					ImageIndex = Common.GetImageIndex(assetType)
				};
				node.Nodes.Add(LoadingNode());
				LoadChildrenBackground(node);
				AddRootNode(node);
			});
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
						txtServer.Select();
						Common.ShowWarning("Please enter a server");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtInstance.Text))
					{
						txtInstance.Select();
						Common.ShowWarning("Please enter an instance name");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtDeveloperKey.Text))
					{
						txtDeveloperKey.Select();
						Common.ShowWarning("Please enter a developer key");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtUsername.Text))
					{
						txtUsername.Select();
						Common.ShowWarning("Please enter a username");
						result = false;
					}
					else if (string.IsNullOrWhiteSpace(txtPassword.Text))
					{
						txtPassword.Select();
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
					if (!cbxExportAssets.Checked && !cbxExportLibraries.Checked && !cbxExportModels.Checked && !cbxExportTemplates.Checked)
					{
						Common.ShowWarning("Please pick at least one type of item to export");
						result = false;
					}
					else if (!TreeHasCheckedNode(treeViewAssets.Nodes))
					{
						Common.ShowWarning("Please select at least one asset to export");
						result = false;
					}
					break;
				case 2:
					if (string.IsNullOrWhiteSpace(txtExportTo.Text))
					{
						txtExportTo.Select();
						Common.ShowWarning("Please enter a filename for the export");
						result = false;
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
			// Recurse down to check our children
			return TreeHasCheckedNode(node.Nodes);
		}

		private bool IsExcluded(CmsResource asset)
		{
			var assetType = asset.AssetType;
			if (assetType.HasFlag(CmsAssetType.LibraryFolder) && !cbxExportLibraries.Checked) return true;
			if (assetType.HasFlag(CmsAssetType.Template) && !cbxExportTemplates.Checked) return true;
			if (assetType.HasFlag(CmsAssetType.Model) && !cbxExportModels.Checked) return true;
			if ((assetType.HasFlag(CmsAssetType.ContentAsset)
			     || assetType.HasFlag(CmsAssetType.DigitalAsset)) && !cbxExportAssets.Checked) return true;
			return false;
		}

		private void LoadSettings()
		{
			var sessions = AppSettings.GetConnections(SettingsType.ForExport);
			cboSessions.Items.Add("");
			if (sessions.Length > 0)
			{
				cboSessions.Items.AddRange(sessions.Select(s => new ComboItem(s)).ToArray());
			}

			var lastSession = AppSettings.GetLastConnection(SettingsType.ForExport);
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
			AppSettings.SaveConnection(cmsInstance, SettingsType.ForExport);
		}

		#endregion

		#region Form Event Handlers

		private void tabWizard_SelectedIndexChanged(object sender, EventArgs e)
		{
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
				Common.ShowError("There was an error verifying your details");
		}

		private void btnRefreshFolders_Click(object sender, EventArgs e)
		{
			RefreshFolders();
		}

		private void txtExportTo_TextChanged(object sender, EventArgs e)
		{
			UpdateUIStatus();
		}

		private void treeViewAssets_AfterExpand(object sender, TreeViewEventArgs e)
		{
			LoadChildrenBackground(e.Node);
		}

		private void treeViewAssets_AfterCheck(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Checked)
				LoadDescendentsBackground(e.Node, true);
			else
			{
				if (_checkTreeInProgress) return;

				_checkTreeInProgress = true;
				CheckTree(e.Node, false);
				_checkTreeInProgress = false;
			}
		}

		private void btnExportTo_Click(object sender, EventArgs e)
		{
			var popup = new SaveFileDialog()
			{
				AddExtension = true,
				AutoUpgradeEnabled = true,
				DefaultExt = "xml",
				FileName = txtExportTo.Text,
				Filter = "Xml files (*.xml)|*.xml|All files (*.*)|*.*",
				OverwritePrompt = true,
				Title = "Choose Export Location",
				ValidateNames = true
			};
			if (popup.ShowDialog() == DialogResult.OK)
			{
				txtExportTo.Text = popup.FileName;
			}
			UpdateUIStatus();
		}

		private void btnGo_Click(object sender, EventArgs e)
		{
			if (VerifyReady()) ExportData();
		}

		private void txtTopFolder_KeyDown(object sender, KeyEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(txtTopFolder.Text) && e.KeyCode == Keys.Return)
			{
				RefreshFolders();
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}

		private void btnSaveLog_Click(object sender, EventArgs e)
		{
			var popup = new SaveFileDialog()
			{
				AddExtension = true,
				AutoUpgradeEnabled = true,
				DefaultExt = "txt",
				FileName = txtExportTo.Text.Replace(".xml", ".txt"),
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

		private void Export_Load(object sender, EventArgs e)
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
			AppSettings.ClearConnections(SettingsType.ForExport);
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
					e.Handled = true;
				}
				else
				{
					e.SuppressKeyPress = true;
					SendKeys.Send("{TAB}");
				}
				e.SuppressKeyPress = true;
			}
			else
			{
				ResetCmsConnection();
			}
		}

		private void txtExportTo_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return && !string.IsNullOrWhiteSpace(txtExportTo.Text))
			{
				btnExportTo_Click(btnExportTo, new EventArgs());
				e.Handled = true;
				e.SuppressKeyPress = true;
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
