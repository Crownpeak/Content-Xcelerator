using System;
using System.Linq;
using System.Xml;
using Crownpeak.WcoApiHelper;

namespace Crownpeak.ContentXcelerator.Migrator
{
	static class WcoXmlHelper
	{
		public static void ExportConnector(Connector connector, XmlNode node, string nodeName)
		{
			var xml = node is XmlDocument ? node as XmlDocument : node.OwnerDocument;
			var fieldNode = node.AppendChild(xml.CreateElement(nodeName));
			fieldNode.AppendChild(xml.CreateElement("id")).InnerText = connector.Id;
			fieldNode.AppendChild(xml.CreateElement("name")).InnerText = connector.Name;
			fieldNode.AppendChild(xml.CreateElement("type")).InnerText = connector.Type.ToString().ToLowerInvariant();
			fieldNode.AppendChild(xml.CreateElement("url")).InnerText = connector.Url;
			if (connector.Fields.Any())
			{
				var valuesNode = fieldNode.AppendChild(xml.CreateElement("fields"));
				foreach (var value in connector.Fields)
				{
					var valueNode = valuesNode.AppendChild(xml.CreateElement("field"));
					valueNode.AppendChild(xml.CreateElement("key")).InnerText = value.Key;
					valueNode.AppendChild(xml.CreateElement("value")).InnerText = value.Value;
				}
			}
		}

		public static void ExportField(Field field, XmlNode node, string nodeName)
		{
			var xml = node is XmlDocument ? node as XmlDocument : node.OwnerDocument;
			var fieldNode = node.AppendChild(xml.CreateElement(nodeName));
			fieldNode.AppendChild(xml.CreateElement("id")).InnerText = field.Id;
			fieldNode.AppendChild(xml.CreateElement("name")).InnerText = field.Name;
			fieldNode.AppendChild(xml.CreateElement("label")).InnerText = field.Label;
			fieldNode.AppendChild(xml.CreateElement("maxLength")).InnerText = field.MaxLength.ToString();
			fieldNode.AppendChild(xml.CreateElement("initialValue")).InnerText = field.InitialValue;
			fieldNode.AppendChild(xml.CreateElement("required")).InnerText = field.Required.ToString().ToLowerInvariant();
			fieldNode.AppendChild(xml.CreateElement("type")).InnerText = field.Type.ToString();
			if (field.FieldValues.Any())
			{
				var valuesNode = fieldNode.AppendChild(xml.CreateElement("values"));
				foreach (var value in field.FieldValues)
				{
					var valueNode = valuesNode.AppendChild(xml.CreateElement("value"));
					valueNode.AppendChild(xml.CreateElement("key")).InnerText = value.Key;
					valueNode.AppendChild(xml.CreateElement("value")).InnerText = value.Value;
				}
			}
			fieldNode.AppendChild(xml.CreateElement("placeholder")).InnerText = field.Placeholder;
			fieldNode.AppendChild(xml.CreateElement("validPattern")).InnerText = field.ValidPattern;
		}

		public static void ExportForm(Form form, XmlNode node, string nodeName)
		{
			var xml = node is XmlDocument ? node as XmlDocument : node.OwnerDocument;
			var fieldNode = node.AppendChild(xml.CreateElement(nodeName));
			fieldNode.AppendChild(xml.CreateElement("id")).InnerText = form.Id;
			fieldNode.AppendChild(xml.CreateElement("name")).InnerText = form.Name;
			fieldNode.AppendChild(xml.CreateElement("secure")).InnerText = form.Secure.ToString().ToLowerInvariant();
			fieldNode.AppendChild(xml.CreateElement("doNotStoreSubmissionData")).InnerText = form.DoNotStoreSubmissionData.ToString().ToLowerInvariant();
			fieldNode.AppendChild(xml.CreateElement("validateEmailRecipientsAgainstWhiteList")).InnerText = form.ValidateEmailRecipientsAgainstWhiteList.ToString().ToLowerInvariant();
			if (form.FormElements.Any())
			{
				var valuesNode = fieldNode.AppendChild(xml.CreateElement("formElements"));
				foreach (var value in form.FormElements)
				{
					var valueNode = valuesNode.AppendChild(xml.CreateElement("element"));
					valueNode.AppendChild(xml.CreateElement("key")).InnerText = value.Key;
					valueNode.AppendChild(xml.CreateElement("value")).InnerText = value.Value;
				}
			}
			if (form.HiddenFields.Any())
			{
				var valuesNode = fieldNode.AppendChild(xml.CreateElement("hiddenFields"));
				foreach (var value in form.HiddenFields)
				{
					var valueNode = valuesNode.AppendChild(xml.CreateElement("field"));
					valueNode.AppendChild(xml.CreateElement("key")).InnerText = value.Key;
					valueNode.AppendChild(xml.CreateElement("value")).InnerText = value.Value;
				}
			}
		}

		public static BehavioralRule ImportBehavioralRule(XmlNode node)
		{
			return new BehavioralRule
			{
				Id = node.SelectSingleNode("id").InnerText,
				RuleType = (WcoApiHelper.BehavioralRuleType)Enum.Parse(typeof(WcoApiHelper.BehavioralRuleType), node.SelectSingleNode("ruleType").InnerText),
				Data = node.SelectSingleNode("data").InnerText,
				Referrer = node.SelectSingleNode("referrer").InnerText,
				Threshold = int.Parse(node.SelectSingleNode("threshold").InnerText)
			};
		}

		public static BehavioralRule[] ImportBehavioralRules(XmlNodeList nodes)
		{
			return (from XmlNode node in nodes select ImportBehavioralRule(node)).ToArray();
		}

		public static Connector ImportConnector(XmlNode node)
		{
			return new WcoApiHelper.Connector
			{
				Id = node.SelectSingleNode("id").InnerText,
				Name = node.SelectSingleNode("name").InnerText,
				Type = int.Parse(node.SelectSingleNode("type").InnerText),
				Url = node.SelectSingleNode("url").InnerText,
				Fields = ImportFieldValues(node.SelectNodes("fields/field"))
			};
		}

		public static Field ImportField(XmlNode node)
		{
			return new Field
			{
				Id = node.SelectSingleNode("id").InnerText,
				Name = node.SelectSingleNode("name").InnerText,
				Label = node.SelectSingleNode("label").InnerText,
				MaxLength = int.Parse(node.SelectSingleNode("maxLength").InnerText),
				InitialValue = node.SelectSingleNode("initialValue").InnerText,
				Required = node.SelectSingleNode("required").InnerText == "true",
				Type = (FieldType)Enum.Parse(typeof(FieldType), node.SelectSingleNode("type").InnerText),
				Placeholder = node.SelectSingleNode("placeholder").InnerText,
				ValidPattern = node.SelectSingleNode("validPattern").InnerText,
				FieldValues = ImportFieldValues(node.SelectNodes("values/value"))
			};
		}

		public static FieldValue ImportFieldValue(XmlNode node)
		{
			return new FieldValue(node.SelectSingleNode("key").InnerText, node.SelectSingleNode("value").InnerText);
		}

		public static FieldValue[] ImportFieldValues(XmlNodeList nodes)
		{
			return (from XmlNode node in nodes select ImportFieldValue(node)).ToArray();
		}

		public static FieldValueUpper ImportFieldValueUpper(XmlNode node)
		{
			return new FieldValueUpper(node.SelectSingleNode("key").InnerText, node.SelectSingleNode("value").InnerText);
		}

		public static FieldValueUpper[] ImportFieldValueUppers(XmlNodeList nodes)
		{
			return (from XmlNode node in nodes select ImportFieldValueUpper(node)).ToArray();
		}

		public static Form ImportForm(XmlNode node)
		{
			return new Form
			{
				Id = node.SelectSingleNode("id").InnerText,
				Name = node.SelectSingleNode("name").InnerText,
				Secure = node.SelectSingleNode("secure").InnerText == "true",
				DoNotStoreSubmissionData = node.SelectSingleNode("doNotStoreSubmissionData").InnerText == "true",
				ValidateEmailRecipientsAgainstWhiteList = node.SelectSingleNode("validateEmailRecipientsAgainstWhiteList").InnerText == "true",
				FormElements = ImportFieldValues(node.SelectNodes("formElements/element")),
				HiddenFields = ImportFieldValueUppers(node.SelectNodes("hiddenFields/field"))
			};
		}

		public static Rule ImportRule(XmlNode node)
		{
			return new WcoApiHelper.Rule
			{
				FieldId = node.SelectSingleNode("fieldId").InnerText,
				Op = (RuleType)Enum.Parse(typeof(RuleType), node.SelectSingleNode("op").InnerText),
				Value = node.SelectSingleNode("value").InnerText,
				Order = int.Parse(node.SelectSingleNode("order").InnerText)
			};
		}

		public static Rule[] ImportRules(XmlNodeList nodes)
		{
			return (from XmlNode node in nodes select ImportRule(node)).ToArray();
		}

		public static Snippet ImportSnippet(XmlNode node)
		{
			return new Snippet
			{
				Id = node.SelectSingleNode("id").InnerText,
				Name = node.SelectSingleNode("name").InnerText,
				HasTargetingVariant = node.SelectSingleNode("hasTargetingVariant").InnerText == "true",
				HasTestingVariant = node.SelectSingleNode("hasTestingVariant").InnerText == "true",
				Variants = ImportVariants(node.SelectNodes("variants/variant"))
			};
		}

		public static TargetGroup ImportTargetGroup(XmlNode node)
		{
			return new TargetGroup
			{
				Id = node.SelectSingleNode("id").InnerText,
				Name = node.SelectSingleNode("name").InnerText,
				Rules = ImportRules(node.SelectNodes("rules/rule")),
				BehavioralRules = ImportBehavioralRules(node.SelectNodes("behavioralRules/rule"))
			};
		}

		public static Wco.Variant ImportVariant(XmlNode node)
		{
			return new Wco.Variant
			{
				Id = node.SelectSingleNode("id").InnerText,
				OriginalId = node.SelectSingleNode("id").InnerText,
				Name = node.SelectSingleNode("name").InnerText,
				Order = int.Parse(node.SelectSingleNode("order").InnerText),
				Weight = int.Parse(node.SelectSingleNode("weight").InnerText),
				SnippetVariant = int.Parse(node.SelectSingleNode("snippetVariant").InnerText),
				TargetGroupId = node.SelectSingleNode("targetGroupId").InnerText,
				EmbedCode = node.SelectSingleNode("content").InnerText,
				Archived = false,
				Deleted = false,
				IsCMSManaged = false,
				CMSAssetId = -1
			};
		}

		public static Wco.Variant[] ImportVariants(XmlNodeList nodes)
		{
			return (from XmlNode node in nodes select ImportVariant(node)).ToArray();
		}
	}
}
