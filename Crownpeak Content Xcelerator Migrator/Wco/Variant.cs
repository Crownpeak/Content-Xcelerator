using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Crownpeak.ContentXcelerator.Migrator.Wco
{
	[DataContract]
	public class Variant : WcoApiHelper.Variant
	{
		public string OriginalId { get; set; }

		public Variant()
		{ }

		public Variant(WcoApiHelper.Variant variant)
		{
			Archived = variant.Archived;
			CMSAssetId = variant.CMSAssetId;
			DateCreated = variant.DateCreated;
			Deleted = variant.Deleted;
			EmbedCode = variant.EmbedCode;
			Id = variant.Id;
			IsCMSManaged = variant.IsCMSManaged;
			Name = variant.Name;
			OmmId = variant.OmmId;
			Order = variant.Order;
			SnippetId = variant.SnippetId;
			SnippetVariant = variant.SnippetVariant;
			TargetGroupId = variant.TargetGroupId;
			Weight = variant.Weight;
			if (variant is Wco.Variant)
			{
				OriginalId = ((Wco.Variant) variant).OriginalId;
			}
		}
	}
}
