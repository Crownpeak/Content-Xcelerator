namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	class ComboItem
	{
		public CmsInstance CmsInstance { get; set; }

		public ComboItem(CmsInstance cmsInstance)
		{
			CmsInstance = cmsInstance;
		}

		public override string ToString()
		{
			return $"{CmsInstance.Server}/{CmsInstance.Instance}";
		}
	}
}
