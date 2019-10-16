using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CrownPeak.AccessApiHelper;
using CrownPeak.AccessApiHelper.ApiAccessor;
using CrownPeak.AccessAPI;
using Crownpeak.WcoApiHelper;
using Crownpeak.WcoApiHelper.ApiAccessor;
using Connector = Crownpeak.WcoApiHelper.Connector;
using Field = Crownpeak.WcoApiHelper.Field;
using Form = Crownpeak.WcoApiHelper.Form;
using IApiAccessor = CrownPeak.AccessApiHelper.ApiAccessor.IApiAccessor;
using RuleType = Crownpeak.WcoApiHelper.RuleType;
using Snippet = Crownpeak.WcoApiHelper.Snippet;
using TargetGroup = Crownpeak.WcoApiHelper.TargetGroup;
using Variant = Crownpeak.WcoApiHelper.Variant;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class MigrationEngine
	{
		private const int CACHE_SIZE = 1000;

		private bool _loggedIn;
		private CmsApi _api;
		private WcoApi _wco = null;
		private CmsAssetCache _cache;
		private Connector[] _connectors = null;
		private Field[] _fields = null;
		private Form[] _forms = null;
		private WcoApiHelper.Snippet[] _snippets = null;
		private TargetGroup[] _targetGroups = null;
		private Dictionary<int, WorkflowData> _workflowsById;
		private Dictionary<string, int> _workflowsByName;
		private Dictionary<int, WorkflowFilter> _workflowFiltersById;
		private Dictionary<string, int> _workflowFiltersByName;
		private Dictionary<string, bool> _workflowsWithDuplicateName;
		private Dictionary<int, PublishingPackage> _packagesById;
		private Dictionary<string, int> _packagesByName;
		private static Regex reForm = new Regex("<input(?:(?:[^>]+name=\"WcoFormId\"[^>]+value=['\"]?(?<formid>[^\"'/\\s]+)['\"]?[^>]+)|(?:[^>]+value=['\"]?(?<formid>[^\"'/\\s]+)['\"]?[^>]+name=\"WcoFormId\"[^>]+)[/]?)>", RegexOptions.IgnoreCase);
		private static Regex reVariant = new Regex("http[s]://snippet.omm.crownpeak.com/p/(?<variantid>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})", RegexOptions.IgnoreCase);

		private static Regex regex = new Regex("/(?<instance>[A-Za-z0-9]+)/cpt_internal/(?<path>([0-9]+/)*)(?<id>[0-9]+)", RegexOptions.None);
		private static Regex regex2 = new Regex("^(/[A-Za-z0-9 _.\\-]+){2,}/?$", RegexOptions.None);
		private static Regex regex3 = new Regex("^([0-9]{4,})$", RegexOptions.None);
		
		public MigrationEngine(CmsInstance cmsInstance)
		{
			IApiAccessor accessor = new SimpleApiAccessor();
			_api = new CmsApi(accessor);
			_api.Init(cmsInstance.Server, cmsInstance.Instance, cmsInstance.Key, "");

			_api.Login(cmsInstance.Username, cmsInstance.Password);

			_wco = null;
			if (!string.IsNullOrWhiteSpace(cmsInstance.WcoUsername) && !string.IsNullOrWhiteSpace(cmsInstance.WcoPassword))
			{
				_wco = new WcoApi(new Crownpeak.WcoApiHelper.ApiAccessor.ApiAccessor());
				_wco.Login(cmsInstance.WcoUsername, cmsInstance.WcoPassword);
			}

			_cache = new CmsAssetCache(CACHE_SIZE, _api, _wco);
		}


		public static bool Authenticate(CmsInstance cmsInstance)
		{
			IApiAccessor accessor = new SimpleApiAccessor();
			var api = new CmsApi(accessor);
			api.Init(cmsInstance.Server, cmsInstance.Instance, cmsInstance.Key, "");


			if (cmsInstance.Password.Length == 0 || cmsInstance.Username.Length == 0)
			{
				return false;
			}

			if (!api.Login(cmsInstance.Username, cmsInstance.Password))
			{
				return false;
			}

			return true;
		}

		public static bool WcoAuthenticate(CmsInstance cmsInstance)
		{
			var wco = new WcoApi(new Crownpeak.WcoApiHelper.ApiAccessor.ApiAccessor());

			if (cmsInstance.WcoPassword.Length == 0 || cmsInstance.WcoUsername.Length == 0)
			{
				return false;
			}

			if (!wco.Login(cmsInstance.WcoUsername, cmsInstance.WcoPassword))
			{
				return false;
			}

			return true;
		}

		public void EndSession()
		{
			if (_api == null) return;
			if (!_loggedIn) return;

			_api.Logout();
			_api = null;
			_loggedIn = false;
		}

		public void Export(ExportSession exportSession)
		{
			//if (Authenticate(exportSession.Instance))
			//{
			// exportSession.LogEntry("", "Authentication successful", EventLogEntryType.Information);
			var assetCollection = ProcessResource(exportSession);

			var xmlDoc = GenerateXmlDocument(assetCollection, exportSession);

			OutputToFile(xmlDoc, exportSession);
			//}
			//else
			//{
			//    exportSession.LogEntry("", "Authentication failed", EventLogEntryType.FailureAudit);
			//}
		}

		private Connector[] GetConnectors()
		{
			if (_connectors != null) return _connectors;

			_connectors = new Connector[0];
			if (_wco != null)
			{
				if (!_wco.Connectors.GetConnectors(out _connectors))
					_connectors = new Connector[0];
			}
			return _connectors;
		}

		private Field[] GetFields()
		{
			if (_fields != null) return _fields;

			_fields = new Field[0];
			if (_wco != null)
			{
				if (!_wco.Fields.GetFields(out _fields))
					_fields = new Field[0];
			}
			return _fields;
		}

		private Form[] GetForms()
		{
			if (_forms != null) return _forms;

			_forms = new Form[0];
			if (_wco != null)
			{
				if (!_wco.Forms.GetForms(out _forms))
					_forms = new Form[0];
			}
			return _forms;
		}

		private WcoApiHelper.Snippet[] GetSnippets()
		{
			if (_snippets != null) return _snippets;

			_snippets = new WcoApiHelper.Snippet[0];
			if (_wco != null)
			{
				if (!_wco.Snippets.GetActiveSnippets(out _snippets))
					_snippets = new WcoApiHelper.Snippet[0];
			}
			return _snippets;
		}

		private WcoApiHelper.TargetGroup[] GetTargetGroups()
		{
			if (_targetGroups != null) return _targetGroups;

			_targetGroups = new TargetGroup[0];
			if (_wco != null)
			{
				if (!_wco.TargetGroups.GetTargetGroups(out _targetGroups))
				_targetGroups = new TargetGroup[0];
			}
			return _targetGroups;
		}

		private static void OutputToFile(XmlNode xml, ExportSession exportSession)
		{
			try
			{
				string result;
				using (var ms = new MemoryStream())
				{
					using (var xtw = new XmlTextWriter(ms, Encoding.Unicode))
					{
						xtw.Formatting = Formatting.Indented;
						xml.WriteContentTo(xtw);
						xtw.Flush();
						ms.Flush();

						ms.Position = 0;

						using (var sr = new StreamReader(ms))
						{
							result = sr.ReadToEnd();
						}
					}
				}

				File.WriteAllText(exportSession.FileLocation, result);
				exportSession.LogEntry("", "Export File generated: " + exportSession.FileLocation, EventLogEntryType.Information);

			}
			catch (Exception ex)
			{
				exportSession.LogEntry("", "Failed to generate output file: " + exportSession.FileLocation + ". " + ex, EventLogEntryType.Information);

			}
		}

		private XmlDocument GenerateXmlDocument(IEnumerable<WorklistAsset> assetCollection, ExportSession exportSession)
		{
			var xml = new XmlDocument();
			xml.AppendChild(xml.CreateProcessingInstruction("xml", "version=\"1.0\""));

			var assetsXml = xml.CreateElement("assets");
			var attribute = xml.CreateAttribute("xmlns:xsi");
			attribute.Value = "http://www.w3.org/2001/XMLSchema-instance";
			assetsXml.Attributes.Append(attribute);
			attribute = xml.CreateAttribute("xsi", "noNamespaceSchemaLocation", "http://www.w3.org/2001/XMLSchema-instance");
			attribute.Value = "https://raw.githubusercontent.com/Crownpeak/Content-Xcelerator/master/Crownpeak%20Content%20Xcelerator%20Migrator/Content-Xcelerator.xsd";
			assetsXml.Attributes.Append(attribute);
			xml.AppendChild(assetsXml);

			var i = 1;
			var worklistAssets = assetCollection as WorklistAsset[] ?? assetCollection.ToArray();
			var total = worklistAssets.Count();

			foreach (var asset in worklistAssets)
			{
				exportSession.LogEntry("Asset: " + asset.id, "Generating XML.", EventLogEntryType.Information, i, total);
				try
				{
					var fieldCollection = _api.Asset.Fields(asset.id);
					GenerateXml(asset, fieldCollection, assetsXml, asset.id == exportSession.TargetFolder);
				}
				catch (Exception ex)
				{
					exportSession.LogEntry("Asset: " + asset.id, "Error occured Generating XML: " + ex, EventLogEntryType.Error, i, total);
				}

				i++;
			}

			DecorateXml(xml);

			return xml;

		}

		private void GenerateXml(WorklistAsset asset, IEnumerable<KeyValuePair<string, string>> fieldCollection, XmlNode parent, bool isTop = false)
		{
			var xml = parent is XmlDocument ? parent as XmlDocument : parent.OwnerDocument;
			var top = xml.SelectSingleNode("/assets");
			XmlNode node;

			if (asset.type.HasValue && asset.type == 4) // folder
			{
				node = parent.AppendChild(xml.CreateElement("folder"));
				node.AppendChild(xml.CreateElement("id")).InnerText = asset.id.ToString();
				node.AppendChild(xml.CreateElement("label")).InnerText = asset.label;
				if (isTop)
					node.AppendChild(xml.CreateElement("isTop")).InnerText = "true";
				node.AppendChild(xml.CreateElement("model_id")).InnerText = asset.model_id.HasValue
						? asset.model_id.ToString()
						: "";
				if (asset.model_id.HasValue && asset.model_id.Value > 0)
				{
					var model = _cache.GetAsset(asset.model_id.Value, true);
					if (model != null && !string.IsNullOrWhiteSpace(model.FullPath))
						node.AppendChild(xml.CreateElement("model_path")).InnerText =
							model.FullPath;
				}
				node.AppendChild(xml.CreateElement("is_deleted")).InnerText = (asset.is_deleted.HasValue &&
																																			 asset.is_deleted.Value
						? "true"
						: "false");
				node.AppendChild(xml.CreateElement("is_hidden")).InnerText = (asset.is_hidden.HasValue &&
																																			asset.is_hidden.Value
						? "true"
						: "false");
				node.AppendChild(xml.CreateElement("path")).InnerText = asset.FullPath;
				node.AppendChild(xml.CreateElement("folder_id")).InnerText = asset.folder_id.HasValue
						 ? asset.folder_id.ToString()
						: "";
				node.AppendChild(xml.CreateElement("subType")).InnerText = asset.subtype.HasValue
						? asset.subtype.ToString()
					 : "";
				node.AppendChild(xml.CreateElement("templateSubType")).InnerText = asset.TemplateSubType.HasValue
					 ? asset.TemplateSubType.ToString()
					: "";

				node.AppendChild(xml.CreateElement("intendedType")).InnerText = GetAssetType(asset, node.OwnerDocument).ToString();

				node.AppendChild(xml.CreateElement("type")).InnerText = asset.type.HasValue
					? asset.type.ToString()
				 : "";

				var options = _cache.GetFolderOptions(asset.id);
				if (options != null)
				{
					if (!string.IsNullOrWhiteSpace(options.Header))
						node.AppendChild(xml.CreateElement("header")).InnerText = options.Header;
					if (options.Type != FolderOptionsType.Folder)
						node.AppendChild(xml.CreateElement("folder_type")).InnerText = options.Type.ToString();
				}

				var publishingProperties = _cache.GetPublishingProperties(asset.id);
				if (publishingProperties != null && publishingProperties.Any())
				{
					var pp = node.AppendChild(xml.CreateElement("publishing_properties"));
					foreach (var publishingProperty in publishingProperties)
					{
						var prop = pp.AppendChild(xml.CreateElement("property"));
						prop.AppendChild(xml.CreateElement("package")).InnerText = publishingProperty.Package;
						prop.AppendChild(xml.CreateElement("type")).InnerText = publishingProperty.Type.ToString();
						prop.AppendChild(xml.CreateElement("filepath")).InnerText = publishingProperty.FilePath;
						prop.AppendChild(xml.CreateElement("filename")).InnerText = publishingProperty.FileName;
						prop.AppendChild(xml.CreateElement("extension")).InnerText = publishingProperty.Extension;
						prop.AppendChild(xml.CreateElement("layout")).InnerText = publishingProperty.Layout;
					}
				}
			}
			else
			{
				if (!(asset.is_deleted.HasValue && asset.is_deleted == true)
						&& !(asset.is_hidden.HasValue && asset.is_hidden == true))
				{
					node = parent.AppendChild(xml.CreateElement("asset"));
					node.AppendChild(xml.CreateElement("id")).InnerText = asset.id.ToString();
					node.AppendChild(xml.CreateElement("label")).InnerText = asset.label;
					node.AppendChild(xml.CreateElement("template_id")).InnerText = asset.template_id.ToString();
					if (asset.template_id.HasValue && asset.template_id.Value > 0)
					{
						var template = _cache.GetAsset(asset.template_id.Value, true);
						if (template != null && !string.IsNullOrWhiteSpace(template.FullPath))
							node.AppendChild(xml.CreateElement("template_path")).InnerText =
								template.FullPath;
					}
					node.AppendChild(xml.CreateElement("template_language")).InnerText =
							asset.template_language.ToString();
					if (asset.model_id.HasValue)
					{
						node.AppendChild(xml.CreateElement("model_id")).InnerText = asset.model_id.Value.ToString();
					}
					else if (asset.base_model_id.HasValue)
					{
						node.AppendChild(xml.CreateElement("model_id")).InnerText = asset.base_model_id.Value.ToString();
					}
					else
					{
						node.AppendChild(xml.CreateElement("model_id")).InnerText = "";
					}
					node.AppendChild(xml.CreateElement("is_deleted")).InnerText = (asset.is_deleted.HasValue &&
																																				 asset.is_deleted.Value
							? "true"
							: "false");
					node.AppendChild(xml.CreateElement("is_hidden")).InnerText = (asset.is_hidden.HasValue &&
																																				asset.is_hidden.Value
							? "true"
							: "false");
					node.AppendChild(xml.CreateElement("workflow_id")).InnerText = asset.workflow_id.HasValue
							? asset.workflow_id.ToString()
							: "";
					if (asset.workflow_id.HasValue && asset.workflow_id.Value > 0)
					{
						var workflow = _cache.GetWorkflow(asset.workflow_id.Value);
						if (workflow != null && !string.IsNullOrWhiteSpace(workflow.Name))
							node.AppendChild(xml.CreateElement("workflow_name")).InnerText = workflow.Name;
					}
					node.AppendChild(xml.CreateElement("status")).InnerText = asset.status.HasValue
							? asset.status.ToString()
							: "";
					if (asset.status.HasValue)
					{
						node.AppendChildElement("status_name", _cache.GetStatusName(asset.status.Value));
					}
					node.AppendChild(xml.CreateElement("path")).InnerText = asset.FullPath;
					node.AppendChild(xml.CreateElement("folder_id")).InnerText = asset.folder_id.HasValue
							 ? asset.folder_id.ToString()
							: "";

					var intendedType = GetAssetType(asset, node.OwnerDocument);
					var subType = asset.subtype;
					// Fix for Component Library creating classes with subtype = 1
					if (intendedType.HasFlag(CmsAssetType.LibraryClass)) subType = 17;

					node.AppendChild(xml.CreateElement("subType")).InnerText = subType.HasValue
						? subType.ToString()
						: "";

					node.AppendChild(xml.CreateElement("templateSubType")).InnerText = asset.TemplateSubType.HasValue
						 ? asset.TemplateSubType.ToString()
						: "";

					node.AppendChild(xml.CreateElement("intendedType")).InnerText = intendedType.ToString();

					node.AppendChild(xml.CreateElement("type")).InnerText = asset.type.HasValue
						? asset.type.ToString()
					 : "";

					if (asset.branchId > 0)
						node.AppendChildElement("branch_id", asset.branchId);

					var fields = node.AppendChild(xml.CreateElement("fields"));

					foreach (var kvp in fieldCollection)
					{
						var field = fields.AppendChild(xml.CreateElement("field"));
						field.AppendChild(xml.CreateElement("name")).InnerText = kvp.Key;
						field.AppendChild(xml.CreateElement("value")).InnerText = kvp.Value;

						if (_wco != null && kvp.Key.StartsWith("ommsnippetid#") && !string.IsNullOrWhiteSpace(kvp.Value))
						{
							if (_wco.Snippets.GetSnippetWithVariants(kvp.Value, true, out var variants))
							{
								if (_wco.Snippets.GetSnippetOverview(kvp.Value, out var snippet))
								{
									if (string.IsNullOrWhiteSpace(snippet.Id)) snippet.Id = snippet.SnippetId;
									if (top.SelectSingleNode("snippet[id='" + snippet.Id + "']") == null)
									{
										var snippetNode = top.AppendChild(xml.CreateElement("snippet"));
										snippetNode.AppendChild(xml.CreateElement("id")).InnerText = snippet.Id;
										snippetNode.AppendChild(xml.CreateElement("name")).InnerText = snippet.Name;
										snippetNode.AppendChild(xml.CreateElement("hasTestingVariant")).InnerText = snippet.HasTestingVariant.ToString().ToLowerInvariant();
										snippetNode.AppendChild(xml.CreateElement("hasTargetingVariant")).InnerText = snippet.HasTargetingVariant.ToString().ToLowerInvariant();
										var variantsNode = snippetNode.AppendChild(xml.CreateElement("variants"));
										foreach (var variant in variants.Where(v => !v.Archived && !v.Deleted))
										{
											var variantNode = variantsNode.AppendChild(xml.CreateElement("variant"));
											variantNode.AppendChild(xml.CreateElement("id")).InnerText = variant.Id;
											variantNode.AppendChild(xml.CreateElement("name")).InnerText = variant.Name;
											variantNode.AppendChild(xml.CreateElement("order")).InnerText = variant.Order.ToString();
											variantNode.AppendChild(xml.CreateElement("weight")).InnerText = variant.Weight.ToString();
											variantNode.AppendChild(xml.CreateElement("snippetVariant")).InnerText = variant.SnippetVariant.ToString();
											variantNode.AppendChild(xml.CreateElement("targetGroupId")).InnerText = variant.TargetGroupId;
											var targetGroup = GetTargetGroups().FirstOrDefault(g => g.Id == variant.TargetGroupId);
											if (targetGroup != null && top.SelectSingleNode("targetGroup[id='" + targetGroup.Id + "']") == null)
											{
												var targetGroupNode = top.AppendChild(xml.CreateElement("targetGroup"));
												targetGroupNode.AppendChild(xml.CreateElement("id")).InnerText = targetGroup.Id;
												targetGroupNode.AppendChild(xml.CreateElement("name")).InnerText = targetGroup.Name;
												if (targetGroup.Rules.Length > 0)
												{
													var rulesNode = targetGroupNode.AppendChild(xml.CreateElement("rules"));
													foreach (var rule in targetGroup.Rules)
													{
														var ruleNode = rulesNode.AppendChild(xml.CreateElement("rule"));
														ruleNode.AppendChild(xml.CreateElement("fieldId")).InnerText = rule.FieldId;
														var wcoField = GetFields().FirstOrDefault(f => f.Id == rule.FieldId);
														if (wcoField != null && top.SelectSingleNode("field[id='" + wcoField.Id + "']") == null)
														{
															WcoXmlHelper.ExportField(wcoField, top, "field");
														}

														ruleNode.AppendChild(xml.CreateElement("op")).InnerText = rule.Op.ToString();
														ruleNode.AppendChild(xml.CreateElement("value")).InnerText = rule.Value;
														// Special values for integrations
														if (rule.Value != null && rule.Value.IndexOf("$CP$") >= 0)
														{
															var values = rule.Value.Split(new[] {"$CP$"}, StringSplitOptions.None);
															if (values.Length >= 2)
															{
																var fieldId = values[1];
																if (fieldId.Length == 36)
																{
																	// Could be a guid, so look up that field
																	wcoField = GetFields().FirstOrDefault(f => f.Id == fieldId);
																	if (wcoField != null && top.SelectSingleNode("field[id='" + wcoField.Id + "']") == null)
																	{
																		WcoXmlHelper.ExportField(wcoField, top, "field");
																	}
																}
															}
														}

														ruleNode.AppendChild(xml.CreateElement("order")).InnerText = rule.Order.ToString();
													}
												}

												if (targetGroup.BehavioralRules.Length > 0)
												{
													var rulesNode = targetGroupNode.AppendChild(xml.CreateElement("behavioralRules"));
													foreach (var rule in targetGroup.BehavioralRules)
													{
														var ruleNode = rulesNode.AppendChild(xml.CreateElement("rule"));
														ruleNode.AppendChild(xml.CreateElement("id")).InnerText = rule.Id;
														ruleNode.AppendChild(xml.CreateElement("ruleType")).InnerText = rule.RuleType.ToString();
														ruleNode.AppendChild(xml.CreateElement("data")).InnerText = rule.Data;
														ruleNode.AppendChild(xml.CreateElement("referrer")).InnerText = rule.Referrer;
														ruleNode.AppendChild(xml.CreateElement("threshold")).InnerText = rule.Threshold.ToString();
													}
												}
											}

											variantNode.AppendChild(xml.CreateElement("content")).InnerText = variant.EmbedCode;

											if (variant.EmbedCode.IndexOf("<form", StringComparison.InvariantCultureIgnoreCase) >= 0
											    && variant.EmbedCode.IndexOf("WcoFormId", StringComparison.InvariantCultureIgnoreCase) >= 0)
											{
												foreach (Match match in reForm.Matches(variant.EmbedCode))
												{
													var formId = match.Groups["formid"].Value;
													if (!string.IsNullOrWhiteSpace(formId))
													{
														var form = GetForms().FirstOrDefault(f => f.Id == formId);
														if (form != null && top.SelectSingleNode("form[id='" + form.Id + "']") == null)
														{
															WcoXmlHelper.ExportForm(form, top, "form");
															var connectorField = form.HiddenFields.FirstOrDefault(f => f.Key == "WcoConnectorId");
															if (connectorField != null && !string.IsNullOrWhiteSpace(connectorField.Value))
															{
																var connector = GetConnectors().FirstOrDefault(c => c.Id == connectorField.Value);
																if (connector != null && top.SelectSingleNode("connector[id='" + connector.Id + "']") == null)
																{
																	WcoXmlHelper.ExportConnector(connector, top, "connector");
																}
															}

															foreach (var element in form.FormElements)
															{
																var wcoField = GetFields().FirstOrDefault(f => f.Name == element.Key && (int) f.Type == int.Parse(element.Value));
																if (wcoField != null && top.SelectSingleNode("field[id='" + wcoField.Id + "']") == null)
																{
																	WcoXmlHelper.ExportField(wcoField, top, "field");
																}
															}
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}

					if (GetAssetType(asset, node.OwnerDocument).HasFlag(CmsAssetType.DigitalAsset))
					{
						string data;
						string filename;
						if (_api.Asset.DownloadBase64(asset.id, out filename, out data))
						{
							node.AppendChild(xml.CreateElement("binaryContent")).InnerText = data;
						}
					}

					if (GetAssetType(asset, node.OwnerDocument).HasFlag(CmsAssetType.Workflow))
					{
						WorkflowAsset workflow;
						if (_api.Workflow.ReadFull(asset.id, out workflow))
						{
							var workflowNode = node.AppendChild(xml.CreateElement("workflow"));
							workflowNode.AppendChildElement("id", workflow.Id);
							workflowNode.AppendChildElement("name", workflow.Name);
							workflowNode.AppendChildElement("description", workflow.Description);

							var stepsNode = workflowNode.AppendChild(xml.CreateElement("steps"));
							foreach (var step in workflow.Steps)
							{
								var stepNode = stepsNode.AppendChild(xml.CreateElement("step"));
								stepNode.AppendChildElement("execFilePath", step.ExecFilePath);
								stepNode.AppendChildElement("accessFile", step.AccessFile);
								stepNode.AppendChildElement("emailFilePath", step.EmailFilePath);

								var commandsNode = stepNode.AppendChild(xml.CreateElement("commands"));
								foreach (var command in step.Commands)
								{
									var commandNode = commandsNode.AppendChild(xml.CreateElement("command"));
									commandNode.AppendChildElement("id", command.Id);
									commandNode.AppendChildElement("command", command.Command);
									commandNode.AppendChildElement("commandDest", command.CommandDest);
									commandNode.AppendChildElement("commandId", command.CommandId);
									commandNode.AppendChildElement("filterId", command.FilterId);
									if (command.FilterId != 0)
									{
										var filter = _cache.GetWorkflowFilter(command.FilterId);
										if (filter != null)
											commandNode.AppendChildElement("filterName", filter.Name);
									}
									commandNode.AppendChildElement("requestComment", (command.RequestComment ?? false));
									commandNode.AppendChildElement("inSummary", (command.InSummary ?? false));
									commandNode.AppendChildElement("queueCommand", (command.QueueCommand ?? false));
									commandNode.AppendChildElement("enforceSpellcheck", (command.EnforceSpellcheck ?? false));
									commandNode.AppendChildElement("enforceEdit", (command.EnforceEdit ?? false));
									commandNode.AppendChildElement("enforceSchedule", (command.EnforceSchedule ?? false));
									commandNode.AppendChildElement("verifyCommand", (command.VerifyCommand ?? false));
									commandNode.AppendChildElement("inEdit", (command.InEdit ?? false));
								}

								var publishesNode = stepNode.AppendChild(xml.CreateElement("publishes"));
								foreach (var publish in step.Publishes)
								{
									var publishNode = publishesNode.AppendChild(xml.CreateElement("publish"));
									publishNode.AppendChildElement("packageId", publish.PublishingServerId);
									publishNode.AppendChildElement("packageName", _cache.GetPublishingPackageName(publish.PublishingServerId));
									publishNode.AppendChildElement("status", publish.Status);
									publishNode.AppendChildElement("statusName", _cache.GetStatusName(publish.Status));
								}

								if (step.Actions.Any())
								{
									var actionsNode = stepNode.AppendChild(xml.CreateElement("actions"));
									foreach (var action in step.Actions)
									{
										var actionNode = actionsNode.AppendChild(xml.CreateElement("action"));
										actionNode.AppendChildElement("id", action.Id);
										actionNode.AppendChildElement("dest", action.Dest);
									}
								}

								if (step.Schedules.Any())
								{
									var schedulesNode = stepNode.AppendChild(xml.CreateElement("schedules"));
									foreach (var schedule in step.Schedules)
									{
										var scheduleNode = schedulesNode.AppendChild(xml.CreateElement("schedule"));
										scheduleNode.AppendChildElement("name", schedule.Name);
										scheduleNode.AppendChildElement("dest", schedule.Dest);
										scheduleNode.AppendChildElement("offset", schedule.Offset);
									}
								}

								if (step.Periodics.Any())
								{
									var periodicsNode = stepNode.AppendChild(xml.CreateElement("periodics"));
									foreach (var periodic in step.Periodics)
									{
										var periodicNode = periodicsNode.AppendChild(xml.CreateElement("periodic"));
										periodicNode.AppendChildElement("dest", periodic.Dest);
										periodicNode.AppendChildElement("minute", periodic.Minute);
										periodicNode.AppendChildElement("hour", periodic.Hour);
										periodicNode.AppendChildElement("weekday", periodic.Weekday);
										periodicNode.AppendChildElement("monthweek", periodic.Monthweek);
										periodicNode.AppendChildElement("frequency", periodic.Frequency);
										periodicNode.AppendChildElement("nextRefresh", periodic.NextRefresh);
										periodicNode.AppendChildElement("timezone", periodic.Timezone);
									}
								}

								if (step.Bookmarks.Any())
								{
									var bookmarksNode = stepNode.AppendChild(xml.CreateElement("bookmarks"));
									foreach (var bookmark in step.Bookmarks)
									{
										var bookmarkNode = bookmarksNode.AppendChild(xml.CreateElement("bookmark"));
										bookmarkNode.AppendChildElement("name", bookmark.Name);
										bookmarkNode.AppendChildElement("value", bookmark.Value);
									}
								}

								if (step.Fields.Any())
								{
									var fieldsNode = stepNode.AppendChild(xml.CreateElement("fields"));
									foreach (var field in step.Fields)
									{
										var fieldNode = fieldsNode.AppendChild(xml.CreateElement("field"));
										fieldNode.AppendChildElement("name", field.Name);
										fieldNode.AppendChildElement("value", field.Value);
									}
								}

								stepNode.AppendChildElement("step", step.Step);
								stepNode.AppendChildElement("taskSubject", step.TaskSubject);
								stepNode.AppendChildElement("taskDescription", step.TaskDescription);
								stepNode.AppendChildElement("status", step.Status);
								stepNode.AppendChildElement("statusName", _cache.GetStatusName(step.Status));
								stepNode.AppendChildElement("setAsDeleted", step.SetAsDeleted);
								stepNode.AppendChildElement("setAsHidden", step.SetAsHidden);
								stepNode.AppendChildElement("publishState", step.PublishState);
								if (step.AfterHours.HasValue && step.AfterHours.Value > 0)
								{
									stepNode.AppendChildElement("afterHours", step.AfterHours.Value);
									stepNode.AppendChildElement("afterGoto", (step.AfterGoto ?? "").Trim());
								}
								if (step.ConflictStep.HasValue)
									stepNode.AppendChildElement("conflictStep", step.ConflictStep);
								if (step.BranchStep.HasValue)
									stepNode.AppendChildElement("branchStep", step.BranchStep);
								stepNode.AppendChildElement("inMenu", step.InMenu ?? false);
								if (step.UseDqm.HasValue)
								{
									stepNode.AppendChildElement("useDqm", step.UseDqm);
									if (step.UseDqm.Value)
									{
										stepNode.AppendChildElement("dqmCheckType", step.DqmCheckType);
										stepNode.AppendChildElement("dqmPercentage", step.DqmPercentage);
									}
								}
							}
						}
					}
				}
			}
		}

		public void Import(ImportSession importSession)
		{
			//Check import file

			importSession.LogEntry("", "Import session started. File: " + importSession.FileLocation, EventLogEntryType.Information);
			var xml = new XmlDocument();

			try
			{
				xml.Load(importSession.FileLocation);

			}
			catch (Exception ex)
			{
				importSession.LogEntry("", "Failed to load import file: " + importSession.FileLocation + ". " + ex, EventLogEntryType.Information);
				return;
			}

			// This is the asset that they exported as the top level
			var exportedTopFolderId = -1;
			var topFolder = xml.SelectSingleNode("//folder[isTop='true']");
			if (topFolder != null)
				exportedTopFolderId = GetNodeIntValue(topFolder.SelectSingleNode("id")).Value;

			//Extract in correct order
			var siteCollection = ProcessXml(xml, CmsAssetType.Site, null, importSession, exportedTopFolderId);
			var projectCollection = ProcessXml(xml, CmsAssetType.Project, null, importSession, exportedTopFolderId);
			var workflowCollection = ProcessXml(xml, CmsAssetType.Workflow, null, importSession, exportedTopFolderId);
			var libraryFolderCollection = ProcessXml(xml, CmsAssetType.LibraryFolder, null, importSession, exportedTopFolderId);
			var libraryClassCollection = ProcessXml(xml, CmsAssetType.LibraryClass, null, importSession, exportedTopFolderId);
			var templatesFolderCollection = ProcessXml(xml, CmsAssetType.TemplatesFolder, null, importSession, exportedTopFolderId);
			var templateFolderCollection = ProcessXml(xml, CmsAssetType.TemplateFolder, null, importSession, exportedTopFolderId);
			var templateCollection = ProcessXml(xml, CmsAssetType.Template, null, importSession, exportedTopFolderId);
			var modelCollection = ProcessXml(xml, CmsAssetType.Model, null, importSession, exportedTopFolderId);
			var folderCollection = ProcessXml(xml, CmsAssetType.Folder, new[] { CmsAssetType.Model }, importSession, exportedTopFolderId);
			var contentCollection = ProcessXml(xml, new[] { CmsAssetType.ContentAsset, CmsAssetType.DigitalAsset }, new[] { CmsAssetType.Model }, importSession, exportedTopFolderId);

			// Make a second pass through the assets we imported
			RelinkAssets(xml, importSession);

			// Recompile any library folders we might have affected
			RecompileLibraries(xml, importSession);

			// Rename any TMF relationships that we imported
			RenameTmfRelationships(xml, importSession);
		}

		private void DecorateXml(XmlDocument xml)
		{
			// Add additional information to the XML as required

			// Find every model path
			var modelPaths = new Dictionary<string, bool>();
			foreach (XmlElement modelPathNode in xml.SelectNodes("//model_path"))
			{
				var path = modelPathNode.InnerText;
				if (!modelPaths.ContainsKey(path)) modelPaths.Add(path, true);
			}

			// Now mark every asset with this path as a model
			foreach (var modelPath in modelPaths.Keys)
			{
				var modelNode = xml.SelectSingleNode("//*[path='" + modelPath.TrimEnd(new[] { '/' }) + "']") as XmlElement;
				if (modelNode != null)
				{
					if (modelNode.Name == "asset")
						modelNode = xml.SelectSingleNode("//folder[id='" + modelNode.SelectSingleNode("folder_id").InnerText + "']") as XmlElement;
				}

				if (modelNode != null)
				{
					SetAsModel(modelNode);
				}
			}

			// Also mark every folder under a Models folder as a model
			foreach (XmlNode modelFolder in xml.SelectNodes("//folder[label='Models']"))
			{
				SetAsModel(modelFolder);
			}
		}

		private void SetAsModel(XmlNode node)
		{
			CmsAssetType assetType;
			if (CmsAssetType.TryParse(node.SelectSingleNode("intendedType").InnerText, out assetType))
			{
				if (!assetType.HasFlag(CmsAssetType.Model))
				{
					node.SelectSingleNode("intendedType").InnerText = (assetType | CmsAssetType.Model).ToString();
					foreach (XmlNode childNode in node.OwnerDocument.SelectNodes("//*[folder_id='" + node.SelectSingleNode("id").InnerText + "']"))
					{
						SetAsModel(childNode);
					}
				}
			}
		}

		private IEnumerable<WorklistAsset> ProcessXml(XmlDocument xml, CmsAssetType includeCmsAssetType, IEnumerable<CmsAssetType> excludeCmsAssetTypes, ImportSession importSession, int exportedTopFolderId)
		{
			return ProcessXml(xml, new[] { includeCmsAssetType }, excludeCmsAssetTypes, importSession, exportedTopFolderId);
		}

		private IEnumerable<WorklistAsset> ProcessXml(XmlDocument xml, IEnumerable<CmsAssetType> includeCmsAssetTypes, IEnumerable<CmsAssetType> excludeCmsAssetTypes, ImportSession importSession, int exportedTopFolderId)
		{
			if (excludeCmsAssetTypes == null)
				excludeCmsAssetTypes = new CmsAssetType[0];
			var assetCollection = new List<WorklistAsset>();
			XmlNodeList assetNodeCollection = null;

			assetNodeCollection = ExtractNodesByIntendedTypes(xml, includeCmsAssetTypes.Select(t => t.ToString()), excludeCmsAssetTypes.Select(t => t.ToString()));
			assetCollection.AddRange(ExtractAssets(assetNodeCollection, importSession, exportedTopFolderId));

			return assetCollection;

		}

		private IEnumerable<WorklistAsset> ExtractAssets(XmlNodeList assetNodeCollection, ImportSession importSession, int exportedTopFolderId)
		{
			var selectedResources = importSession.ResourceCollection;

			var results = new List<WorklistAsset>();

			if (assetNodeCollection != null)
			{
				foreach (XmlNode node in assetNodeCollection)
				{
					var id = int.Parse(node.SelectSingleNode("id").InnerText);
					var resource = selectedResources.FirstOrDefault(r => r.AssetId == id);
					if (resource != null)
					{
						var assetType = (CmsAssetType)Enum.Parse(typeof(CmsAssetType), node.SelectSingleNode("intendedType").InnerText);

						var type = GetNodeIntValue(node.SelectSingleNode("type"));

						var folderId = GetNodeIntValue(node.SelectSingleNode("folder_id")).Value;
						// Get the real folder id for the target
						folderId = GetFolderId(importSession.TargetFolder, folderId, exportedTopFolderId, resource, selectedResources);
						var label = GetNodeStringValue(node.SelectSingleNode("label"));

						var templateId = resource.TemplateId;
						var modelId = resource.ModelId;
						var templateLanguage = GetNodeIntValue(node.SelectSingleNode("template_language"));
						var templatePath = GetNodeStringValue(node.SelectSingleNode("template_path"));
						var base64data = GetNodeStringValue(node.SelectSingleNode("binaryContent"));
						var subType = GetNodeIntValue(node.SelectSingleNode("subType"));
						if (!string.IsNullOrWhiteSpace(templatePath) && templateId == null)
						{
							// Last chance to find a template for this item

							// Get the original template id
							var originalTemplateId = GetNodeIntValue(node.SelectSingleNode("template_id"));
							if (originalTemplateId != null)
							{
								var foundTemplate = importSession.ResourceCollection.FirstOrDefault(r => r.AssetId == originalTemplateId && r.Asset != null);
								if (foundTemplate != null)
								{
									templateId = foundTemplate.Asset.id;
								}
							}
							else
							{
								// TODO: this path probably won't be the same - need to map
								var templateAsset = _cache.GetAsset(templatePath);
								if (templateAsset != null)
									templateId = templateAsset.id;
							}
						}
						if (!string.IsNullOrWhiteSpace(templatePath)
								&& ((templatePath.Equals("/System/Templates/Basis/Developer", StringComparison.OrdinalIgnoreCase)
									&& templateLanguage == 0)
								|| (templatePath.Equals("/System/Templates/Basis/DeveloperCS", StringComparison.OrdinalIgnoreCase)
									&& templateLanguage == 1)))
						{
							// TODO: a better way to recognise Developer and DeveloperCS items
							templateId = null;
						}
						else
						{
							templateLanguage = null;
						}
						var workflowId = resource.WorkflowId;
						if (workflowId == null)
						{
							var workflowName = GetNodeStringValue(node.SelectSingleNode("workflow_name"));
							if (!string.IsNullOrWhiteSpace(workflowName) && importSession.WorkflowFilterMaps.ContainsKey(workflowName))
							{
								workflowId = importSession.WorkflowFilterMaps[workflowName];
							}
						}

						var fields = GetFields(node);

						// TODO: validate we have everything we need

						WorklistAsset asset;
						try
						{
							var modified = ImportAsset(assetType, folderId, label, importSession.OverwriteExistingAssets,
								templateId, templateLanguage, type.Value, subType, modelId, workflowId, fields, base64data, importSession, resource, node, out asset);

							// Update the resource with our asset
							resource.Asset = asset;
							resource.OkToRelink = true; // DEBUG modified;
							if (modified)
							{
								importSession.LogEntry("Asset: " + id, $"Imported asset as {asset.FullPath} ({asset.id})",
									EventLogEntryType.Information, selectedResources.Count(r => r.Asset != null), selectedResources.Count());
							}
							else
							{
								importSession.LogEntry("Asset: " + id, $"Skipped, already exists as {asset.FullPath} ({asset.id})",
									EventLogEntryType.Information, selectedResources.Count(r => r.Asset != null), selectedResources.Count());
							}

							if (assetType.HasFlag(CmsAssetType.Model))
							{
								// Add this model id to resources that use this
								var originalId = GetNodeIntValue(node.SelectSingleNode("id"));
								// Find nodes that use this model
								foreach (XmlNode nodeUsingModel in node.OwnerDocument.SelectNodes("//*[model_id='" + originalId + "']"))
								{
									var resourceUsingModel = selectedResources.FirstOrDefault(r => r.AssetId == GetNodeIntValue(nodeUsingModel.SelectSingleNode("id")));
									if (resourceUsingModel != null)
									{
										resourceUsingModel.ModelId = asset.id;
										if (resourceUsingModel.Asset != null)
										{
											// We should also set the model on the asset that was already created
											try
											{
												_api.AssetProperties.SetModel(resourceUsingModel.Asset.id, asset.id);
											}
											catch (Exception)
											{
												// We don't really care if this fails - it's not so important
											}
										}
									}
								}
							}

							results.Add(asset);
						}
						catch (Exception ex)
						{
							importSession.LogEntry("Asset: " + id, string.Format("Error creating asset - {0}", ex.Message),
								EventLogEntryType.Error, selectedResources.Count(r => r.Asset != null), selectedResources.Count());
						}
					}
				}
			}

			return results;
		}

		private void RelinkAssets(XmlDocument xml, ImportSession importSession)
		{
			var resourcesForRelinking = importSession.ResourceCollection.Where(r => r.OkToRelink
				&& r.Asset != null
				&& (r.AssetType.HasFlag(CmsAssetType.ContentAsset)
					|| r.AssetType.HasFlag(CmsAssetType.LibraryClass)
					|| r.AssetType.HasFlag(CmsAssetType.Template))).ToList();
			if (!resourcesForRelinking.Any()) return;

			importSession.LogEntry("", "Starting relinking.", EventLogEntryType.Information);

			//var regex = new Regex("/(?<instance>[A-Za-z0-9]+)/cpt_internal/(?<path>([0-9]+/)*)(?<id>[0-9]+)", RegexOptions.None);
			//var regex2 = new Regex("^(/[A-Za-z0-9 _.\\-]+){2,}/?$", RegexOptions.None);
			//var regex3 = new Regex("^([0-9]{4,})$", RegexOptions.None);

			foreach (var resource in resourcesForRelinking)
			{
				var originalAssetId = resource.AssetId;
				var node = xml.SelectSingleNode("//asset[id='" + originalAssetId + "']");
				if (node != null)
				{
					var originalFields = GetFields(node);
					var fields = new Dictionary<string, string>();
					var fieldsToDelete = new List<string>();
					foreach (var field in originalFields)
					{
						if (ReplaceLinks(importSession, field.Value, out var newValue))
						{
							fields.Add(field.Key, newValue);
						}
					}

					foreach (var field in originalFields.Where(f => f.Key.StartsWith("ommsnippetid#")))
					{
						var newSnippet = _cache.GetSnippetByOldId(field.Value);
						if (newSnippet != null)
						{
							fields.Add(field.Key, newSnippet.Id);
							// Now find the original variants
							foreach (XmlNode variantNode in xml.SelectNodes("/assets/snippet[id='" + field.Value + "']/variants/variant"))
							{
								var oldVariantId = variantNode.SelectSingleNode("id").InnerText;
								var newVariant = _cache.GetVariantByOldId(oldVariantId);
								if (newVariant != null)
								{
									var content = originalFields.ContainsKey("ommvarcont#" + oldVariantId) ? originalFields["ommvarcont#" + oldVariantId] : newVariant.EmbedCode;
									if (ReplaceLinks(importSession, content, out var updatedContent))
									{
										content = updatedContent;
									}
									fields.Add("ommvarcont#" + newVariant.Id, content);
									fieldsToDelete.Add("ommvarcont#" + oldVariantId);
									if (fields.ContainsKey("ommvarcont#" + oldVariantId)) fields.Remove("ommvarcont#" + oldVariantId);
								}
							}
						}
					}

					foreach (var field in originalFields.Where(f => f.Key.StartsWith("ommvariantid#")))
					{
						var newVariant = _cache.GetVariantByOldId(field.Value);
						if (newVariant != null)
						{
							fields.Add(field.Key, newVariant.Id);
						}
					}

					if (fields.Count > 0)
					{
						// Save our changes
						_cache.UpdateAsset(resource.Asset, fields, fieldsToDelete);
						importSession.LogEntry("Asset: " + resource.Asset.id, $"Relinked {fields.Count} field(s)", EventLogEntryType.Information);
					}
				}
			}

			// We have to serialize the dictionary because SaveSnippet will modify it later
			var snippetsCopy = _cache.GetSnippets().ToList();
			foreach (var snippet in snippetsCopy)
			{
				var updated = 0;
				if (_wco.Snippets.GetSnippetWithVariants(snippet.Value.Id, true, out var variants))
				{
					var variantsToSave = variants.Select(v => new Wco.Variant(v)).ToArray();
					foreach (var variant in variantsToSave)
					{
						updated += ReplaceLinks(importSession, variant.EmbedCode, out var newValue) ? 1 : 0;
						variant.EmbedCode = newValue;
						var oldVariantId = _cache.GetOldVariantIdByNewId(variant.Id);
						if (!string.IsNullOrEmpty(oldVariantId))
							variant.OriginalId = oldVariantId;
					}
					
					if (updated > 0)
					{
						_cache.SaveSnippet(snippet.Value.Id, snippet.Value.Name, variantsToSave, true, true);
						importSession.LogEntry("Snippet: " + snippet.Value.Name, $"Relinked {updated} variants(s)", EventLogEntryType.Information);
					}
				}
			}
		}

		private bool ReplaceLinks(ImportSession importSession, string value, out string relinkedValue)
		{
			relinkedValue = value;
			var newValue = value;
			// Shortcut empty fields
			if (string.IsNullOrEmpty(value)) return false;
			var matches = regex.Matches(value);
			if (matches.Count > 0)
			{
				// We found matches, now we need to fix them
				foreach (Match match in matches)
				{
					var id = int.Parse(match.Groups["id"].Value);
					// Find the new asset for this old id
					var newResource = importSession.ResourceCollection.FirstOrDefault(r => r.Asset != null && r.AssetId == id);
					if (newResource != null)
					{
						var replacement = string.Format("/{0}/cpt_internal/{1}", importSession.Instance.Instance, newResource.Asset.id);
						newValue = newValue.Replace(match.Value, replacement);
					}
				}
			}
			else
			{
				// Try a path match instead - used by Component Library
				matches = regex2.Matches(value);
				if (matches.Count > 0)
				{
					// There can only be one match with this one
					var path = matches[0].Value;
					var newResource = importSession.ResourceCollection.FirstOrDefault(r => r.Asset != null
						&& (r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
						|| r.Path.Equals(path.Substring(1), StringComparison.OrdinalIgnoreCase)));
					if (newResource != null)
					{
						relinkedValue = newResource.Asset.FullPath;
						return true;
					}
				}
				else
				{
					// Try a pure id match instead - used by TMF
					// Note there's a risk we could replace a real number here...
					matches = regex3.Matches(value);
					if (matches.Count > 0)
					{
						// There can only be one match with this one
						// Find the new asset for this old id
						var newResource = importSession.ResourceCollection.FirstOrDefault(r => r.Asset != null && r.AssetId.ToString() == value);
						if (newResource != null)
						{
							relinkedValue = newResource.Asset.id.ToString();
							return true;
						}
					}
				}
			}

			// Look for WcoFormId elements
			foreach (Match match in reForm.Matches(newValue))
			{
				var formId = match.Groups["formid"].Value;
				// Find the new form for this old id
				var newForm = _cache.GetFormByOldId(formId);
				if (newForm != null)
				{
					newValue = newValue.Replace(formId, newForm.Id);
				}
			}

			// Look for variants (used to post back forms)
			foreach (Match match in reVariant.Matches(newValue))
			{
				var variantId = match.Groups["variantid"].Value;
				// Find the new form for this old id
				var newVariant = _cache.GetVariantByOldId(variantId);
				if (newVariant != null)
				{
					newValue = newValue.Replace(variantId, newVariant.Id);
				}
			}

			if (newValue != value)
			{
				relinkedValue = newValue;
				return true;
			}

			return false;
		}

		private void RecompileLibraries(XmlDocument xml, ImportSession importSession)
		{
			var librariesForRecompiling = importSession.ResourceCollection
				.Where(r => r.AssetType.HasFlag(CmsAssetType.LibraryFolder)).ToList();
			if (!librariesForRecompiling.Any()) return;

			importSession.LogEntry("", "Starting recompilation.", EventLogEntryType.Information);

			foreach (var library in librariesForRecompiling)
			{
				if (!_api.Tools.RecompileLibrary(library.Asset.id))
				{
					importSession.LogEntry("Asset: " + library.Asset.id, "Failed to recompile " + library.Asset.FullPath, EventLogEntryType.Information);
				}
				else
				{
					importSession.LogEntry("Asset: " + library.Asset.id, "Recompiled " + library.Asset.FullPath, EventLogEntryType.Information);
				}
			}
		}

		private void RenameTmfRelationships(XmlDocument xml, ImportSession importSession)
		{
			var tmfFolders = importSession.ResourceCollection.Where(r => r.Path.EndsWith("_tmf/relationships config", StringComparison.OrdinalIgnoreCase)
				|| r.Path.EndsWith("_cdf/relationships config", StringComparison.OrdinalIgnoreCase));
			if (!tmfFolders.Any()) return;

			importSession.LogEntry("", "Starting renaming TMF relationships.", EventLogEntryType.Information);

			foreach (var tmfFolder in tmfFolders)
			{
				foreach (XmlNode relationshipNode in xml.SelectNodes("//asset[folder_id=\"" + tmfFolder.AssetId + "\"]"))
				{
					var relationship = importSession.ResourceCollection.FirstOrDefault(r => r.Asset != null && r.AssetId == GetNodeIntValue(relationshipNode.SelectSingleNode("id")));
					if (relationship != null)
					{
						var fields = GetFields(relationshipNode);
						if (fields.ContainsKey("source_id") && fields.ContainsKey("destination_id"))
						{
							var newSource = importSession.ResourceCollection.FirstOrDefault(r => r.Asset != null && r.AssetId.ToString() == fields["source_id"]);
							var newDestination = importSession.ResourceCollection.FirstOrDefault(r => r.Asset != null && r.AssetId.ToString() == fields["destination_id"]);
							if (newSource != null && newDestination != null)
							{
								if (_api.Asset.Rename(relationship.Asset.id, newSource.Asset.id + "-" + newDestination.Asset.id, out var newRelationship))
								{
									importSession.LogEntry("Asset: " + relationship.Asset.id,
										"Renamed relationship to " + newRelationship.label, EventLogEntryType.Information);
								}
								else
								{
									importSession.LogEntry("Asset: " + relationship.Asset.id,
										"Failed to rename relation", EventLogEntryType.Information);
								}
							}
						}
					}
				}
			}
		}

		private int GetFolderId(int topLevelFolderId, int originalFolderId, int exportedTopFolderId, CmsResource resource, IEnumerable<CmsResource> allResources)
		{
			// First see if we've already worked with this folder
			var foundFolder = allResources.FirstOrDefault(r => r.AssetId == originalFolderId);
			if (foundFolder != null && foundFolder.Asset != null)
			{
				// We've already processed this folder and created an asset for it
				return foundFolder.Asset.id;
			}

			// We need to find or create a folder tree
			var topPath = _cache.GetAsset(topLevelFolderId, true).FullPath;
			var resourcePath = resource.Path;
			var workingPath = topPath;

			var exportedTopResource = allResources.FirstOrDefault(r => r.AssetId == exportedTopFolderId);
			if (exportedTopResource != null)
			{
				// Swap out the full path with the shorter path from our top
				if (resourcePath.StartsWith(exportedTopResource.Path, StringComparison.OrdinalIgnoreCase))
				{
					var folderPath = exportedTopResource.Path.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last();
					resourcePath = string.Concat("/", folderPath, resourcePath.Substring(exportedTopResource.Path.Length));
				}
			}

			var pathSegments = resourcePath.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
			var parentId = topLevelFolderId;

			// If it's a resource inside /System, keep its path completely intact
			if (resource.Path.StartsWith("/System", StringComparison.OrdinalIgnoreCase))
			{
				workingPath = "";
				parentId = 0;
			}

			if (pathSegments.Count > 0)
			{
				// Remove the last item - it'll be created later
				pathSegments.RemoveAt(pathSegments.Count - 1);
				foreach (var pathSegment in pathSegments)
				{
					workingPath += "/" + pathSegment;
					var asset = _cache.GetAsset(workingPath);
					if (asset == null)
					{
						// We need to create this folder
						asset = _cache.CreateAsset(pathSegment, parentId, 0, 4, null, 0, 0, 0, null);
					}
					// Remember this for the next pass
					parentId = asset.id;
				}
				// Return the last folder we found/created
				return parentId;
			}

			return topLevelFolderId;
		}

		private int? GetNodeIntValue(XmlNode node)
		{
			if (node == null) return null;
			var value = node.InnerText;

			int output;
			if (!int.TryParse(value, out output)) return null;
			return output;
		}

		private int GetNodeIntValue(XmlNode node, int defaultValue)
		{
			if (node == null) return defaultValue;
			var value = node.InnerText;

			int output;
			if (!int.TryParse(value, out output)) return defaultValue;
			return output;
		}

		private string GetNodeStringValue(XmlNode node)
		{
			if (node == null) return null;
			return node.InnerText;
		}

		private Dictionary<string, string> GetFields(XmlNode assetNode)
		{
			var fields = new Dictionary<string, string>();

			var fieldNodes = assetNode.SelectNodes("fields/field");
			if (fieldNodes != null)
			{
				foreach (XmlNode fieldNode in fieldNodes)
				{
					var name = GetNodeStringValue(fieldNode.SelectSingleNode("name"));
					var value = GetNodeStringValue(fieldNode.SelectSingleNode("value"));
					if (!string.IsNullOrWhiteSpace(name))
					{
						if (value == null) value = "";
						if (!fields.ContainsKey(name))
							fields.Add(name, value);
						else
						{
							// TODO: what happens now?!
						}
					}
				}
			}

			return fields;
		}

		private XmlNodeList ExtractNodesByIntendedType(XmlDocument xml, string includeIntendedType, IEnumerable<string> excludeIntendedTypes)
		{
			return ExtractNodesByIntendedTypes(xml, new[] { includeIntendedType }, excludeIntendedTypes);
		}

		private XmlNodeList ExtractNodesByIntendedTypes(XmlDocument xml, IEnumerable<string> includeIntendedTypes, IEnumerable<string> excludeIntendedTypes)
		{
			var exclusions = string.Join("", excludeIntendedTypes.Select(t => string.Format("[not(contains(concat(', ', intendedType, ', '), ', {0}, '))]", t)));
			var xpath = string.Join(" | ", includeIntendedTypes.Select(t => string.Format("//*[contains(concat(', ', intendedType, ', '), ', {0}, ')]{1}", t, exclusions)));
			var results = xml.SelectNodes(xpath);
			return results;
		}


		public bool ImportAsset(CmsAssetType assetType, int folderId, string label, bool overwrite, int? templateId, int? templateLanguage, int type, int? subtype, int? modelId, int? workflowId, Dictionary<string, string> fields, string base64data, ImportSession importSession, CmsResource resource, XmlNode node, out WorklistAsset asset)
		{
			// Check for folder existence first
			var folder = _cache.GetAsset(folderId, true);
			if (folder == null)
				throw new Exception("Cannot create asset in non-existent folder " + folderId);

			var created = false;

			// Now check for asset existence
			asset = _cache.GetAsset(string.Concat(folder.FullPath, "/", label));
			if (asset != null && !overwrite)
			{
				// Asset already exists and we're not overwriting
				return false;
			}
			else
			{
				if (asset == null)
				{
					if (assetType.HasFlag(CmsAssetType.Site))
					{
						// Sites need special treatment
						// Note that we're not allowed to create site roots inside site roots
						// But we need to, because otherwise Component Library doesn't work
						// So we make the site root in the root folder, then move it
						var originalLabel = "";
						int ignore;
						if (_api.Asset.Exists("/" + label, out ignore))
						{
							originalLabel = label;
							label = "Site" + new Random().NextDouble().ToString().Split(".".ToCharArray()).Last();
						}
						asset = _cache.CreateSite(label, 0);
						if (asset == null)
							throw new Exception("Error creating site root");
						if (folderId != 0)
						{
							if (!_api.Asset.Move(asset.id, folderId, out asset) || asset == null)
								throw new Exception("Error moving site root");
							if (!string.IsNullOrEmpty(originalLabel))
							{
								if (!_api.Asset.Rename(asset.id, originalLabel, out asset) || asset == null)
									throw new Exception("Error renaming site root");
							}
						}
						created = true;
					}
					else if (assetType.HasFlag(CmsAssetType.Project))
					{
						// As do projects
						asset = _cache.CreateProject(label, folderId);
						if (asset == null)
							throw new Exception("Error creating project");
						created = true;
					}
					else if (assetType.HasFlag(CmsAssetType.Workflow))
					{
						// And workflows are really complex!
						var workflowNode = node.SelectSingleNode("workflow");
						var workflowAsset = new WorkflowAsset
						{
							Name = GetNodeStringValue(workflowNode.SelectSingleNode("name")),
							Description = GetNodeStringValue(workflowNode.SelectSingleNode("description")),
							IsNew = true
						};
						var steps = new List<WorkflowStep>();
						foreach (XmlNode stepNode in workflowNode.SelectNodes("steps/step"))
						{
							var step = new WorkflowStep
							{
								ExecFilePath = GetNodeStringValue(stepNode.SelectSingleNode("execFilePath")) ?? "",
								AccessFile = _cache.GetAsset(importSession.AccessMaps[GetNodeStringValue(stepNode.SelectSingleNode("accessFile"))]).FullPath,
								AccessId = _cache.GetAsset(importSession.AccessMaps[GetNodeStringValue(stepNode.SelectSingleNode("accessFile"))]).id,
								EmailFilePath = "",
								Step = GetNodeIntValue(stepNode.SelectSingleNode("step")).Value,
								TaskSubject = GetNodeStringValue(stepNode.SelectSingleNode("taskSubject")) ?? "",
								TaskDescription = GetNodeStringValue(stepNode.SelectSingleNode("taskDescription")) ?? "",
								Status = importSession.StateMaps[GetNodeStringValue(stepNode.SelectSingleNode("statusName"))],
								SetAsDeleted = GetNodeStringValue(stepNode.SelectSingleNode("setAsDeleted")) == "true",
								SetAsHidden = GetNodeStringValue(stepNode.SelectSingleNode("setAsHidden")) == "true",
								ConflictStep = GetNodeIntValue(stepNode.SelectSingleNode("conflictStep")),
								BranchStep = GetNodeIntValue(stepNode.SelectSingleNode("branchStep")),
								PublishState = GetNodeStringValue(stepNode.SelectSingleNode("publishState")) == "true",
								InMenu = GetNodeStringValue(stepNode.SelectSingleNode("inMenu")) == "true",
								UseDqm = GetNodeStringValue(stepNode.SelectSingleNode("useDqm")) == "true",
								DqmCheckType = 0,
								DqmPercentage = 0,
								AfterHours = GetNodeIntValue(stepNode.SelectSingleNode("afterHours")),
								AfterGoto = GetNodeStringValue(stepNode.SelectSingleNode("afterGoto"))
							};
							if (step.UseDqm.Value == true)
							{
								step.DqmCheckType = GetNodeIntValue(stepNode.SelectSingleNode("dqmCheckType")).Value;
								step.DqmPercentage = GetNodeIntValue(stepNode.SelectSingleNode("dqmPercentage")).Value;
							}

							var commands = new List<WorkflowCommand>();
							foreach (XmlNode commandNode in stepNode.SelectNodes("commands/command"))
							{
								var command = new WorkflowCommand
								{
									Command = GetNodeStringValue(commandNode.SelectSingleNode("command")),
									CommandDest = GetNodeIntValue(commandNode.SelectSingleNode("commandDest")).Value,
									FilterId = GetNodeStringValue(commandNode.SelectSingleNode("filterName")) != null
										? importSession.WorkflowFilterMaps[GetNodeStringValue(commandNode.SelectSingleNode("filterName"))]
										: 0,
									RequestComment = GetNodeStringValue(commandNode.SelectSingleNode("requestComment")) == "true",
									InSummary = GetNodeStringValue(commandNode.SelectSingleNode("inSummary")) == "true",
									QueueCommand = GetNodeStringValue(commandNode.SelectSingleNode("queueCommand")) == "true",
									EnforceSpellcheck = GetNodeStringValue(commandNode.SelectSingleNode("enforceSpellcheck")) == "true",
									EnforceEdit = GetNodeStringValue(commandNode.SelectSingleNode("enforceEdit")) == "true",
									EnforceSchedule = GetNodeStringValue(commandNode.SelectSingleNode("enforceSchedule")) == "true",
									VerifyCommand = GetNodeStringValue(commandNode.SelectSingleNode("verifyCommand")) == "true",
									InEdit = GetNodeStringValue(commandNode.SelectSingleNode("inEdit")) == "true"
								};
								commands.Add(command);
							}
							step.Commands = commands.ToArray();

							var publishes = new List<WorkflowPublish>();
							foreach (XmlNode publishNode in stepNode.SelectNodes("publishes/publish"))
							{
								if (importSession.PackageMaps.ContainsKey(GetNodeStringValue(publishNode.SelectSingleNode("packageName")))
									&& importSession.StateMaps.ContainsKey(GetNodeStringValue(publishNode.SelectSingleNode("statusName"))))
								{
									var publish = new WorkflowPublish
									{
										PublishingServerId =
											importSession.PackageMaps[GetNodeStringValue(publishNode.SelectSingleNode("packageName"))],
										Status = importSession.StateMaps[GetNodeStringValue(publishNode.SelectSingleNode("statusName"))]
									};
									publishes.Add(publish);
								}
							}
							step.Publishes = publishes.ToArray();

							var actions = new List<WorkflowAction>();
							foreach (XmlNode actionNode in stepNode.SelectNodes("actions/action"))
							{
								var action = new WorkflowAction
								{
									Id = GetNodeIntValue(actionNode.SelectSingleNode("id")).Value,
									Dest = GetNodeIntValue(actionNode.SelectSingleNode("dest")).Value
								};
								actions.Add(action);
							}
							step.Actions = actions.ToArray();

							var schedules = new List<WorkflowSchedule>();
							foreach (XmlNode scheduleNode in stepNode.SelectNodes("schedules/schedule"))
							{
								var schedule = new WorkflowSchedule
								{
									Name = GetNodeStringValue(scheduleNode.SelectSingleNode("name")),
									Dest = GetNodeIntValue(scheduleNode.SelectSingleNode("dest")).Value,
									Offset = GetNodeIntValue(scheduleNode.SelectSingleNode("offset")).Value
								};
								schedules.Add(schedule);
							}
							step.Schedules = schedules.ToArray();

							var periodics = new List<WorkflowPeriodic>();
							foreach (XmlNode periodicNode in stepNode.SelectNodes("periodics/periodic"))
							{
								var periodic = new WorkflowPeriodic
								{
									Dest = GetNodeIntValue(periodicNode.SelectSingleNode("dest")).Value,
									Minute = GetNodeIntValue(periodicNode.SelectSingleNode("minute")).Value,
									Hour = GetNodeIntValue(periodicNode.SelectSingleNode("hour")).Value,
									Weekday = GetNodeIntValue(periodicNode.SelectSingleNode("weekday")).Value,
									Monthweek = GetNodeIntValue(periodicNode.SelectSingleNode("monthweek")).Value,
									Frequency = GetNodeIntValue(periodicNode.SelectSingleNode("freqency")).Value,
									NextRefresh = GetNodeStringValue(periodicNode.SelectSingleNode("nextRefresh")),
									Timezone = GetNodeStringValue(periodicNode.SelectSingleNode("timezone")),
								};
								periodics.Add(periodic);
							}
							step.Periodics = periodics.ToArray();

							var workflowFields = new List<WorkflowField>();
							foreach (XmlNode fieldNode in stepNode.SelectNodes("fields/field"))
							{
								var field = new WorkflowField
								{
									Name = GetNodeStringValue(fieldNode.SelectSingleNode("name")),
									Value = GetNodeStringValue(fieldNode.SelectSingleNode("value"))
								};
								workflowFields.Add(field);
							}
							step.Fields = workflowFields.ToArray();

							var bookmarks = new List<WorkflowBookmark>();
							foreach (XmlNode bookmarkNode in stepNode.SelectNodes("bookmark/bookmark"))
							{
								var bookmark = new WorkflowBookmark
								{
									Name = GetNodeStringValue(bookmarkNode.SelectSingleNode("name")),
									Value = GetNodeIntValue(bookmarkNode.SelectSingleNode("value")).Value
								};
								bookmarks.Add(bookmark);
							}
							step.Bookmarks = bookmarks.ToArray();

							steps.Add(step);
						}
						workflowAsset.Steps = steps.ToArray();

						workflowAsset = _cache.CreateWorkflow(workflowAsset, folderId);
						if (workflowAsset == null)
							throw new Exception("Error creating workflow");

						asset = _cache.GetAsset(workflowAsset.AssetId);
						asset.label = workflowAsset.Name;

						if (importSession.WorkflowFilterMaps.ContainsKey(workflowAsset.Name))
						{
							importSession.WorkflowFilterMaps[workflowAsset.Name] = workflowAsset.Id;
						}
						else
						{
							importSession.WorkflowFilterMaps.Add(workflowAsset.Name, workflowAsset.Id);
						}
					}
					else
					{
						var adjustedTemplateLanguage = templateLanguage ?? -1;
						// VB templates are no longer allowed
						if (templateId == 759) templateId = 1792;
						// This is how we pass in a map to DeveloperCS
						if (templateId == 1792) templateId = 0;
						if (templateId == 0) adjustedTemplateLanguage = 1;

						if (assetType.HasFlag(CmsAssetType.DigitalAsset) && string.IsNullOrWhiteSpace(base64data))
						{
							// We can't support zero-byte uploads
							base64data = "QQ=="; // BASE64 encoding of "A"
						}

						if (!string.IsNullOrWhiteSpace(base64data))
						{
							asset = _cache.CreateDigitalAsset(label, folderId, -1, workflowId.HasValue ? workflowId.Value : 0,
								base64data);
							if (asset == null)
								throw new Exception("Error creating digital asset");
							created = true;
						}
						else
						{
							// Disallow different models on templates
							if (assetType.HasFlag(CmsAssetType.TemplateFolder) && modelId != null)
							{
								modelId = null;
							}

							// Null subtype is not allowed for template folders
							if (assetType.HasFlag(CmsAssetType.TemplateFolder) && subtype == null)
							{
								subtype = 512;
							}

							// Some content has templates containing templates, which is illegal
							// Make sure to get the right template subtype
							if (subtype == 512 || subtype == 256)
							{
								var folderChildCount = node.OwnerDocument.SelectNodes("//folder[folder_id='" + node.SelectSingleNode("id").InnerText + "']").Count;
								var fileChildCount = node.OwnerDocument.SelectNodes("//asset[folder_id='" + node.SelectSingleNode("id").InnerText + "']").Count;
								if (subtype == 512 && folderChildCount > 0 && fileChildCount == 0) subtype = 256;
								else if (subtype == 256 && folderChildCount == 0 && fileChildCount > 0) subtype = 512;
							}

							// Create a new asset
							asset = _cache.CreateAsset(label, folderId, modelId ?? -1,
								type, subtype, adjustedTemplateLanguage, templateId ?? 0,
								workflowId.HasValue ? workflowId.Value : 0, fields);
							if (asset == null)
								throw new Exception("Error creating asset");

							ProcessWcoFields(importSession, asset, fields, node, overwrite);

							created = true;
						}
					}
				}
				else
				{
					// Don't send an update if there's nothing to do
					if (fields.Count > 0)
					{
						// Update an existing asset
						asset = _cache.UpdateAsset(asset, fields, new List<string>());
						if (asset == null)
							throw new Exception("Error updating asset");

						ProcessWcoFields(importSession, asset, fields, node, overwrite);
					}
				}
			}

			if (created || overwrite)
			{
				// Folder options
				if (node.SelectSingleNode("header") != null || node.SelectSingleNode("folder_type") != null)
				{
					var headerNode = node.SelectSingleNode("header");
					var header = headerNode == null ? "" : headerNode.InnerText;

					var folderTypeNode = node.SelectSingleNode("folder_type");
					FolderOptionsType folderType;
					if (folderTypeNode == null || !FolderOptionsType.TryParse(folderTypeNode.InnerText, out folderType))
						folderType = FolderOptionsType.Folder;

					if (!_api.AssetProperties.SetFolderOptions(new[] { asset.id }, header, folderType))
						throw new Exception("Error setting folder options");
				}

				// Publishing properties
				if (node.SelectSingleNode("publishing_properties/property/package") != null)
				{
					var publishingProperties = new List<DeploymentRecord>();
					foreach (var property in node.SelectNodes("publishing_properties/property"))
					{
						var propertyNode = (XmlNode)property;
						var packageName = propertyNode.SelectSingleNode("package").InnerText;
						if (importSession.PackageMaps.ContainsKey(packageName))
						{
							var prop = new DeploymentRecord
							{
								PackageId = importSession.PackageMaps[packageName],
								Type = propertyNode.SelectSingleNode("type").InnerText == "Templated"
									? DeploymentType.HtmlDeployment
									: DeploymentType.AssetDeployment,
								Filepath = propertyNode.SelectSingleNode("filepath").InnerText,
								Filename = propertyNode.SelectSingleNode("filename").InnerText,
								Extension = propertyNode.SelectSingleNode("extension").InnerText,
								Layout = propertyNode.SelectSingleNode("layout").InnerText,
							};
							publishingProperties.Add(prop);
						}
					}

					if (publishingProperties.Any())
					{
						if (!_api.AssetProperties.SetPublishingProperties(new[] { asset.id }, publishingProperties))
							throw new Exception("Error setting publishing properties");
					}
				}
			}

			asset.FullPath = string.Concat(folder.FullPath.TrimEnd(new[] { '/' }), "/", asset.label);

			return true;
		}

		private void ProcessWcoFields(ImportSession importSession, WorklistAsset asset, Dictionary<string, string> fields, XmlNode node, bool overwrite)
		{
			if (_wco == null) return;

			foreach (var field in fields.Where(f => f.Key.StartsWith("omm")))
			{
				if (field.Key.StartsWith("ommsnippetid#") && !string.IsNullOrWhiteSpace(field.Value))
				{
					var snippetId = field.Value;
					var snippetNode = node.OwnerDocument.SelectSingleNode("/assets/snippet[id='" + snippetId + "']");
					var snippet = WcoXmlHelper.ImportSnippet(snippetNode);

					foreach (var variant in snippet.Variants)
					{
						// Handle the target groups that each variant uses
						var targetGroupId = variant.TargetGroupId;
						if (targetGroupId != Guid.Empty.ToString())
						{
							var targetGroupNode = node.OwnerDocument.SelectSingleNode("/assets/targetGroup[id='" + targetGroupId + "']");
							var targetGroup = WcoXmlHelper.ImportTargetGroup(targetGroupNode);

							// Handle the fields that each target group uses
							foreach (var rule in targetGroup.Rules)
							{
								var rulefieldNode = node.OwnerDocument.SelectSingleNode("/assets/field[id='" + rule.FieldId + "']");
								if (rulefieldNode != null)
								{ 
									var ruleField = WcoXmlHelper.ImportField(rulefieldNode);

									rule.FieldId = _cache.SaveField(ruleField.Id, ruleField.Name, ruleField.Label, ruleField.MaxLength, ruleField.InitialValue, ruleField.Required, ruleField.Type, ruleField.FieldValues, ruleField.Placeholder, ruleField.ValidPattern, overwrite, false).Id;
								}
								else if (rule.Value.IndexOf("$CP$") >= 0)
								{
									// Rules using integration have value in the format marketokeyfield$CP$wcokeyfieldid$CP$marketofieldname$CP$value
									var values = rule.Value.Split(new[] {"$CP$"}, StringSplitOptions.None);
									if (values.Length > 2)
									{
										rulefieldNode = node.OwnerDocument.SelectSingleNode("/assets/field[id='" + values[1] + "']");
										if (rulefieldNode != null)
										{
											var ruleField = WcoXmlHelper.ImportField(rulefieldNode);

											values[1] = _cache.SaveField(ruleField.Id, ruleField.Name, ruleField.Label, ruleField.MaxLength, ruleField.InitialValue, ruleField.Required, ruleField.Type, ruleField.FieldValues, ruleField.Placeholder, ruleField.ValidPattern, overwrite, false).Id;
											rule.Value = string.Join("$CP$", values);
										}
									}
								}
							}

							// TODO: process behavioral rules data?

							var newTargetGroup = _cache.SaveTargetGroup(targetGroup.Id, targetGroup.Name, targetGroup.Rules, targetGroup.BehavioralRules, overwrite, false);
							variant.TargetGroupId = newTargetGroup.Id;

							if (newTargetGroup.Id != targetGroup.Id || overwrite)
							{
								importSession.LogEntry("WCO", $"Imported target group {newTargetGroup.Name}", EventLogEntryType.Information);
							}
							else if (!overwrite)
							{
								importSession.LogEntry("WCO", $"Skipped target group {newTargetGroup.Name}", EventLogEntryType.Information);
							}

						}

						// Process each form used in the EmbedCode
						variant.EmbedCode = reForm.Replace(variant.EmbedCode, (match) =>
						{
							var formNode = node.OwnerDocument.SelectSingleNode("/assets/form[id='" + match.Groups["formid"].Value + "']");
							var form = WcoXmlHelper.ImportForm(formNode);

							// Handle the connector (if any) that each form uses
							var connectorField = form.HiddenFields.FirstOrDefault(f => f.Key == "WcoConnectorId");
							if (connectorField != null)
							{
								var connectorNode = node.OwnerDocument.SelectSingleNode("/assets/connector[id='" + connectorField.Value + "']");
								var connector = WcoXmlHelper.ImportConnector(connectorNode);

								var newConnector = _cache.SaveConnector(connector.Id, connector.Name, connector.Type, connector.Url, connector.Fields, overwrite, false);
								connectorField.Value = newConnector.Id;

								if (newConnector.Id != connector.Id || overwrite)
								{
									importSession.LogEntry("WCO", $"Imported connector {connector.Name}", EventLogEntryType.Information);
								}
								else if (!overwrite)
								{
									importSession.LogEntry("WCO", $"Skipped connector {connector.Name}", EventLogEntryType.Information);
								}
							}

							// Handle the fields that each form uses
							foreach (var element in form.FormElements)
							{
								var fieldNode = node.OwnerDocument.SelectSingleNode("/assets/field[name='" + element.Key + "']");
								var thisField = WcoXmlHelper.ImportField(fieldNode);

								_cache.SaveField(thisField.Id, thisField.Name, thisField.Label, thisField.MaxLength, thisField.InitialValue, thisField.Required, thisField.Type, thisField.FieldValues, thisField.Placeholder, thisField.ValidPattern, overwrite, false);
							}

							var newForm = _cache.SaveForm(form.Id, form.Name, form.FormElements, form.HiddenFields, form.Secure, form.DoNotStoreSubmissionData, form.ValidateEmailRecipientsAgainstWhiteList, overwrite, false);

							if (newForm.Id != form.Id || overwrite)
							{
								importSession.LogEntry("WCO", $"Imported form {form.Name}", EventLogEntryType.Information);
							}
							else if (!overwrite)
							{
								importSession.LogEntry("WCO", $"Skipped form {form.Name}", EventLogEntryType.Information);
							}

							return match.Value.Replace(match.Groups["formid"].Value, newForm.Id);
						});

						// Attach to the new asset id
						variant.CMSAssetId = asset.id;
						variant.IsCMSManaged = true;
					}

					// Now we can create/update the snippet
					var newSnippet = _cache.SaveSnippet(snippet.Id, snippet.Name, snippet.Variants, overwrite, false);

					if (newSnippet == null)
					{
						importSession.LogEntry("WCO", $"Error importing snippet {snippet.Name}", EventLogEntryType.Error);
					}
					if (newSnippet.Id != snippet.Id || overwrite)
					{
						importSession.LogEntry("WCO", $"Imported snippet {snippet.Name}", EventLogEntryType.Information);
					}
					else if (!overwrite)
					{
						importSession.LogEntry("WCO", $"Skipped snippet {snippet.Name}", EventLogEntryType.Information);
					}
				}
			}
		}

		public WorklistAsset GetAsset(int id, bool ensurePath = false)
		{
			return _cache.GetAsset(id, ensurePath);
		}

		public WorklistAsset GetAsset(string path)
		{
			return _cache.GetAsset(path);
		}

		public IList<CmsResource> GetAssetList(string path)
		{
			int assetId;
			if (!_api.Asset.Exists(path, out assetId))
			{
				throw new Exception("Asset not found");
			}

			return GetAssetList(assetId);

		}

		public IList<CmsResource> GetAssetList(int id)
		{
			IEnumerable<WorklistAsset> pageAssetList;
			var assetList = new List<WorklistAsset>();
			int normalCount, hiddenCount, deletedCount;
			const int pageSize = 50; //Maximum allowed at time of development.
			var page = 0;
			var retrievedCount = 0;

			//Paged retrieval
			do
			{
				_api.Asset.GetList(id, page, pageSize, "", OrderType.Ascending, VisibilityType.Normal, true, true, out pageAssetList,
						out normalCount, out hiddenCount, out deletedCount);

				retrievedCount += pageAssetList.Count();
				page++;

				assetList.AddRange(pageAssetList);
			}
			while (retrievedCount < normalCount);

			_cache.AddList(assetList);

			return assetList.Select(a => new CmsResource
			{
				Asset = a,
				AssetId = a.id,
				AssetType = GetAssetType(a),
				Name = a.label
			}).ToList();
		}

		private static CmsAssetType GetAssetType(WorklistAsset asset, XmlDocument xml = null)
		{
			var cmsAssetType = CmsAssetType.Other;

			if (xml != null)
			{
				// See if we can get a hint from the type of the containing folder
				var intendedTypeNode = xml.SelectSingleNode("//folder[id='" + asset.folder_id + "']/intendedType");
				if (intendedTypeNode != null)
				{
					var intendedType = intendedTypeNode.InnerText;
					if (intendedType.Contains("LibraryFolder")) return CmsAssetType.LibraryClass;
					if (intendedType.Contains("TemplatesFolder")) return CmsAssetType.TemplateFolder;
					if (intendedType.Contains("WorkflowsFolder")) return CmsAssetType.Workflow;
					if (intendedType.Contains("TemplateFolder"))
						return asset.type.HasValue && asset.type == 4
							? CmsAssetType.TemplateFolder
							: CmsAssetType.Template;
				}
			}

			if (asset.FullPath == "/System/Templates") return CmsAssetType.TemplatesFolder;

			switch (asset.subtype)
			{
				case 32:
					cmsAssetType = CmsAssetType.Project;
					break;
				case 64:
					cmsAssetType = CmsAssetType.LibraryFolder;
					break;
				case 128:
					cmsAssetType = CmsAssetType.Site;
					break;
				case 256:
					cmsAssetType = CmsAssetType.TemplatesFolder;
					break;
				case 1024:
					if (asset.type == 4)
						cmsAssetType = CmsAssetType.WorkflowsFolder;
					else
						cmsAssetType = CmsAssetType.Workflow;
					break;
				default:
					if (asset.type == 4)
					{
						cmsAssetType = CmsAssetType.Folder;
					}
					else
						cmsAssetType = asset.template_id == 0 ? CmsAssetType.DigitalAsset : CmsAssetType.ContentAsset;
					break;

			}
			return cmsAssetType;
		}

		public WorkflowData GetWorkflow(int id)
		{
			GetWorkflows();

			if (_workflowsById.ContainsKey(id))
				return _workflowsById[id];

			return null;
		}

		public WorkflowData GetWorkflow(string name)
		{
			GetWorkflows();

			if (_workflowsByName.ContainsKey(name))
			{
				var id = _workflowsByName[name];
				if (_workflowsById.ContainsKey(id))
					return _workflowsById[id];
			}

			return null;
		}

		public bool IsWorkflowDuplicateName(string name)
		{
			GetWorkflows();
			return _workflowsWithDuplicateName.ContainsKey(name);
		}

		public Dictionary<int, WorkflowData> GetWorkflows()
		{
			if (_workflowsById == null)
			{
				if (!_api.Workflow.GetList(out _workflowsById))
				{
					_workflowsById = new Dictionary<int, WorkflowData>();
					_workflowsByName = new Dictionary<string, int>();
				}
				else
				{
					// Keep a dictionary of workflows with duplicate names
					_workflowsWithDuplicateName = _workflowsById.GroupBy(w => w.Value.Name).Where(g => g.Count() > 1)
						.ToDictionary(g => g.Key, g => true);
					// Make a second dictionary so we can find the ID given the name
					_workflowsByName = _workflowsById.GroupBy(w => w.Value.Name).Select(g => g.First()).ToDictionary(w => w.Value.Name, w => w.Key);
				}
			}
			return _workflowsById;
		}

		public WorkflowFilter GetWorkflowFilter(int id)
		{
			GetWorkflowFilters();

			if (_workflowFiltersById.ContainsKey(id))
				return _workflowFiltersById[id];

			return null;
		}

		public WorkflowFilter GetWorkflowFilter(string name)
		{
			GetWorkflowFilters();

			if (_workflowFiltersByName.ContainsKey(name))
			{
				var id = _workflowFiltersByName[name];
				if (_workflowFiltersById.ContainsKey(id))
					return _workflowFiltersById[id];
			}

			return null;
		}

		public Dictionary<int, WorkflowFilter> GetWorkflowFilters()
		{
			if (_workflowFiltersById == null)
			{
				WorkflowFilter[] filters;
				if (!_api.Workflow.GetWorkflowFilters(out filters))
				{
					_workflowFiltersById = new Dictionary<int, WorkflowFilter>();
					_workflowFiltersByName = new Dictionary<string, int>();
				}
				else
				{
					_workflowFiltersById = filters.ToDictionary(f => f.Id, f => f);
					// Make a second dictionary so we can find the ID given the name
					_workflowFiltersByName = filters.ToDictionary(f => f.Name, f => f.Id);
				}
			}
			return _workflowFiltersById;
		}

		public PublishingPackage GetPublishingPackage(int id)
		{
			GetPublishingPackages();

			if (_packagesById.ContainsKey(id))
				return _packagesById[id];

			return null;
		}

		public PublishingPackage GetPublishingPackage(string name)
		{
			GetPublishingPackages();

			if (_packagesByName.ContainsKey(name))
			{
				var id = _packagesByName[name];
				if (_packagesById.ContainsKey(id))
					return _packagesById[id];
			}

			return null;
		}

		public Dictionary<int, PublishingPackage> GetPublishingPackages()
		{
			if (_packagesById == null)
			{
				PublishingPackage[] packages;
				if (!_api.Settings.GetPackages(out packages))
				{
					_packagesById = new Dictionary<int, PublishingPackage>();
					_packagesByName = new Dictionary<string, int>();
				}
				else
				{
					_packagesById = packages.ToDictionary(p => p.Id, p => p);
					// Make a second dictionary so we can find the ID given the name
					_packagesByName = packages.ToDictionary(p => p.Name, p => p.Id);
				}
			}
			return _packagesById;
		}

		private IEnumerable<WorklistAsset> ProcessResource(ExportSession exportSession, CmsAssetType assetType = CmsAssetType.Other)
		{
			if (assetType.HasFlag(CmsAssetType.Other))
			{
				return exportSession.ResourceCollection
					.Select(lc => lc.Asset)
					.ToList();
			}

			return exportSession.ResourceCollection
				.Where(lc => lc.AssetType == assetType)
				.Select(lc => lc.Asset)
				.ToList();
		}
	}
}