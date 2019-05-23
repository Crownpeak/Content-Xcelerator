using System.Collections.Generic;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class ImportSession : MigrationSession
	{
		public IEnumerable<CmsResource> ResourceCollection { get; set; }
		public bool OverwriteExistingAssets { get; set; }
		public Dictionary<string, int> AccessMaps { get; set; }
		public Dictionary<string, int> PackageMaps { get; set; }
		public Dictionary<string, int> StateMaps { get; set; }
		public Dictionary<string, int> WorkflowFilterMaps { get; set; }
	}
}