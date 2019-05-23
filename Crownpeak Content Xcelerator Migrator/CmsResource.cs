using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class CmsResource
	{
		public int AssetId { get; set; }
		public WorklistAsset Asset { get; set; }
		public CmsAssetType AssetType { get; set; }
		public string Name { get; set; }
		public string Path { get; set; }
		public int? TemplateId { get; set; }
		public int? ModelId { get; set; }
		public int? WorkflowId { get; set; }
		public bool OkToRelink { get; set; }
	}
}