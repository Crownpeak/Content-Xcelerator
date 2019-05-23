using System.Xml;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public static class Extensions
	{
		public static XmlNode AppendChildElement(this XmlNode node, string name, string value)
		{
			if (node.OwnerDocument == null) return null;

			var element = node.AppendChild(node.OwnerDocument.CreateElement(name));
			element.InnerText = value;
			return element;
		}

		public static XmlNode AppendChildElement(this XmlNode node, string name, object value)
		{
			if (node.OwnerDocument == null) return null;

			var element = node.AppendChild(node.OwnerDocument.CreateElement(name));
			element.InnerText = value.ToString();
			return element;
		}
	}
}
