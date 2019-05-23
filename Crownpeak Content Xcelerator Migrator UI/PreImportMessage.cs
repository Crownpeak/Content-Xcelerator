using System.ComponentModel;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	class PreImportMessage
	{
		[Browsable(false)]
		public int AssetId { get; set; }
		public string Path { get; set; }
		public MessageStatus Status { get; set; }
		public string Message { get; set; }
		[Browsable(false)]
		public ProblemType Type { get; set; }
		[Browsable(false)]
		public string Key { get; set; }
	}

	public enum MessageStatus
	{
		Ok = 0,
		Warning,
		Error
	}

	public enum ProblemType
	{
		Other = 0,
		Template,
		Model,
		Workflow,
		WorkflowFilter,
		Package,
		State,
		Access
	}
}
