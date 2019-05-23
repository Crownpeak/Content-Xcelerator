namespace Crownpeak.ContentXcelerator.Migrator
{
	class PublishingProperties
	{
		public string Package { get; private set; }
		public PublishingPropertiesType Type { get; private set; }
		public string FilePath { get; private set; }
		public string FileName { get; private set; }
		public string Extension { get; private set; }
		public string Layout { get; private set; }

		public PublishingProperties(string package, PublishingPropertiesType type, string filePath, string fileName, string extension, string layout)
		{
			Package = package.Trim();
			Type = type;
			FilePath = filePath.Trim();
			FileName = fileName.Trim();
			Extension = extension.Trim();
			Layout = layout.Trim();
			if (type == PublishingPropertiesType.Digital) Layout = "";
		}
	}

	enum PublishingPropertiesType
	{
		Unknown = 0,
		Digital,
		Templated
	}
}
