using System.ComponentModel;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	public class PreImportMessageGroup
	{
		public MessageStatus Status { get; set; }
		public string Message { get; set; }
		[Browsable(false)]
		public ProblemType Type { get; set; }
		[Browsable(false)]
		public int[] AssetIds { get; set; }
		public string Resolution { get; set; }
		[Browsable(false)]
		public int? MappedToId { get; set; }
		[Browsable(false)]
		public string Key { get; set; }
	}
}
