using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI.Dialogs
{
	public partial class AssetPicker : Form
	{
		private MigrationEngineWrapper _migrationEngine;
		private const string LOADING = "Loading...";
		private AsyncLock _loadingLock = new AsyncLock();
		private int _loadsInProgress = 0;

		public WorklistAsset SelectedAsset
		{
			get
			{
				var node = treeViewAssets.SelectedNode;
				if (node != null) return (WorklistAsset) node.Tag;
				return null;
			}
		}

		public AssetPicker(MigrationEngineWrapper migrationEngineWrapper)
		{
			_migrationEngine = migrationEngineWrapper;
			InitializeComponent();
		}

		private void AssetPicker_Load(object sender, EventArgs e)
		{
			var asset = _migrationEngine.GetAsset(0);

			var rootNode = new TreeNode("/")
			{
				ImageIndex = Common.GetImageIndex(CmsAssetType.Folder),
				Tag = asset,
				Text = "/"
			};
			rootNode.SelectedImageIndex = rootNode.ImageIndex;
			rootNode.Nodes.Add(LoadingNode());
			LoadChildrenBackground(rootNode);
			treeViewAssets.Nodes.Add(rootNode);
		}

		private void SetChildNodes(TreeNode parent, TreeNode[] children, bool expand)
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
		}

		private TreeNode[] LoadChildren(TreeNode node)
		{
			var result = new List<TreeNode>();
			if (!(node.Nodes.Count == 1 && node.Nodes[0].Text == LOADING)) return result.ToArray();

			var contents = _migrationEngine.GetAssetList(((WorklistAsset)node.Tag).id);

			foreach (var item in contents)
			{
				// Remove a leading "/root" from the path
				if (!string.IsNullOrWhiteSpace(item.Asset.FullPath) && item.Asset.FullPath.StartsWith("/root"))
				{
					item.Asset.FullPath = item.Asset.FullPath.Substring(5);
				}

				var childNode = new TreeNode(item.Name)
				{
					ImageIndex = Common.GetImageIndex(item.AssetType),
					Tag = item.Asset
				};
				childNode.SelectedImageIndex = childNode.ImageIndex;
				if (Common.AssetTypeCanHaveChildren(item.AssetType))
				{
					childNode.Nodes.Add(LoadingNode());
				}
				result.Add(childNode);
			}
			return result.ToArray();
		}

		private TreeNode LoadingNode()
		{
			return new TreeNode(LOADING) {Tag = null};
		}

		private bool HasLoadingNode(TreeNode node)
		{
			return node.Nodes.Count == 1 && node.Nodes[0].Text == LOADING;
		}

		private TreeNode FindChild(TreeNode node, string name)
		{
			if (HasLoadingNode(node))
			{
				// Get the children and expand
				SetChildNodes(node, LoadChildren(node), true);
			}
			else
			{
				node.Expand();
			}

			foreach (TreeNode child in node.Nodes)
			{
				if (child.Text.Equals(name, StringComparison.OrdinalIgnoreCase))
					return child;
			}

			return null;
		}

		private void treeViewAssets_AfterExpand(object sender, TreeViewEventArgs e)
		{
			LoadChildrenBackground(e.Node);
		}

		private void btnOk_Click(object sender, EventArgs e)
		{
			if (treeViewAssets.SelectedNode != null)
			{
				DialogResult = DialogResult.OK;
				Close();
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}

		private void treeViewAssets_AfterSelect(object sender, TreeViewEventArgs e)
		{
			txtPath.Text = e.Node.FullPath.Replace('\\', '/').Substring(1);
		}

		private void btnGo_Click(object sender, EventArgs e)
		{
			var path = txtPath.Text;
			if (!string.IsNullOrWhiteSpace(path))
			{
				if (int.TryParse(path, out var id))
				{
					path = "";
					var asset = _migrationEngine.GetAsset(id, true);
					if (asset != null) path = asset.FullPath;
				}

				if (!string.IsNullOrWhiteSpace(path))
				{
					var pathSegments = path.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
					var node = treeViewAssets.Nodes[0];
					foreach (var segment in pathSegments)
					{
						node = FindChild(node, segment);
						if (node == null) break;
					}

					if (node != null)
					{
						node.TreeView.SelectedNode = node;
						node.EnsureVisible();
						node.TreeView.Focus();
					}
				}
			}
		}

		private void txtPath_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				btnGo.PerformClick();
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}

		private void txtPath_Enter(object sender, EventArgs e)
		{
			AcceptButton = null;
		}

		private void txtPath_Leave(object sender, EventArgs e)
		{
			AcceptButton = btnOk;
		}
	}
}
