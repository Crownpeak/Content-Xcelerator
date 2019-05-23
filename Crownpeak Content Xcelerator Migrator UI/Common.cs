using System.Windows.Forms;
using System.Xml;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	class Common
	{
		public static void ShowInformation(string message, string title = "Information")
		{
			MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		public static void ShowWarning(string message, string title = "Warning")
		{
			MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}

		public static void ShowError(string message, string title = "Error")
		{
			MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		public static int GetImageIndex(CmsAssetType assetType)
		{
			if (assetType.HasFlag(CmsAssetType.LibraryFolder)) return 3;
			if (assetType.HasFlag(CmsAssetType.Site)) return 4;
			if (assetType.HasFlag(CmsAssetType.Project)) return 5;
			if (assetType.HasFlag(CmsAssetType.WorkflowsFolder)) return 6;
			if (assetType.HasFlag(CmsAssetType.Folder)) return 2;
			if (assetType.HasFlag(CmsAssetType.TemplatesFolder)) return 2;
			if (assetType.HasFlag(CmsAssetType.TemplateFolder)) return 2;
			return 1;
		}

		public static bool IsNodeAllowedChildren(int imageIndex)
		{
			return imageIndex == 2 || imageIndex == 3 || imageIndex == 4 || imageIndex == 5;
		}

		public static CmsAssetType GetAssetType(string type)
		{
			CmsAssetType result;
			if (CmsAssetType.TryParse(type, out result))
				return result;
			return CmsAssetType.Other;
		}

		public static CmsAssetType GetAssetType(XmlNode asset)
		{
			if (asset != null)
			{
				var typeNode = asset.SelectSingleNode("intendedType");
				if (typeNode != null)
					return GetAssetType(typeNode.InnerText);
			}

			return CmsAssetType.Other;
		}

		public static bool AssetTypeCanHaveChildren(CmsAssetType assetType)
		{
			return assetType.HasFlag(CmsAssetType.Folder)
			       || assetType.HasFlag(CmsAssetType.Project)
			       || assetType.HasFlag(CmsAssetType.Site)
			       || assetType.HasFlag(CmsAssetType.TemplatesFolder)
			       || assetType.HasFlag(CmsAssetType.TemplateFolder)
			       || assetType.HasFlag(CmsAssetType.LibraryFolder)
						 || assetType.HasFlag(CmsAssetType.WorkflowsFolder);
		}
	}
}
