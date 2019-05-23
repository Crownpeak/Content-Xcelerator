using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator
{
	class FolderOptions
	{
		public string Header { get; private set; }
		public FolderOptionsType Type { get; private set; }

		public FolderOptions(string header, FolderOptionsType type)
		{
			Header = header;
			Type = type;
		}
	}
}
