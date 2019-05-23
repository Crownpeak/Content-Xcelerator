using System;

namespace Crownpeak.ContentXcelerator.Migrator
{
	[Flags]
	public enum CmsAssetType
	{
		None = 0,
		LibraryFolder = 1,
		LibraryClass = 2,
		TemplatesFolder = 4,
		TemplateFolder = 8,
		Template = 16,
		Model = 32,
		ContentAsset = 64,
		Folder = 128,
		DigitalAsset = 256,
		Project = 512,
		Site = 1024,
		WorkflowsFolder = 2048,
		Workflow = 4096,
		Other = 8192
	};
}