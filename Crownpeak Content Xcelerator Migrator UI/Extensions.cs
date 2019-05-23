using System.Windows.Forms;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	static class Extensions
	{
		public static bool HasCheckedDescendents(this TreeNode node)
		{
			if (node.Checked) return true;

			foreach (TreeNode child in node.Nodes)
			{
				if (HasCheckedDescendents(child)) return true;
			}
			return false;
		}
	}
}
