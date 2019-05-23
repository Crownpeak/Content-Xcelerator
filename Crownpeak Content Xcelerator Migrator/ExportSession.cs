using System.Collections.Generic;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class ExportSession : MigrationSession
	{
		public IEnumerable<CmsResource> ResourceCollection { get; set; }
	}
}
