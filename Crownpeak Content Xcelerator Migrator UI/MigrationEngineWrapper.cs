using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	public class MigrationEngineWrapper
	{
		private const uint PROJECT = 32,
			LIBRARY = 64,
			SITE = 128,
			TEMPLATES = 256,
			TEMPLATE = 512,
			WORKFLOWS = 1024,
			STATES = 2048;

		private readonly CmsInstance _cms;
		private readonly MigrationEngine _migrationEngine;
		private Dictionary<string, int> _accessMaps = new Dictionary<string, int>();
		private Dictionary<string, int> _modelMaps = new Dictionary<string, int>();
		private Dictionary<string, int> _packageMaps = new Dictionary<string, int>();
		private Dictionary<string, int> _stateMaps = new Dictionary<string, int>();
		private Dictionary<string, int> _templateMaps = new Dictionary<string, int>();
		private Dictionary<string, int> _workflowMaps = new Dictionary<string, int>();
		private Dictionary<string, int> _workflowFilterMaps = new Dictionary<string, int>();

		public MigrationEngineWrapper(string server, string instance, string developerKey, string username, string password, bool useWco, string wcoUsername, string wcoPassword)
		{
			_cms = new CmsInstance
			{
				Server = server,
				Instance = instance,
				Key = developerKey,
				Username = username,
				Password = password
			};
			if (useWco)
			{
				_cms.WcoUsername = wcoUsername;
				_cms.WcoPassword = wcoPassword;
			}
			_migrationEngine = new MigrationEngine(_cms);
		}

		#region Export Methods

		public IList<LogEntry> Export(string location, int topLevelId, bool includeContent, bool includeLibrary, bool includeModels,
			bool includeTemplates, IEnumerable<CmsResource> resources, EventHandler<MigrationItemEventArgs> onItemProcessed = null)
		{
			var migrationSession = new MigrationSessionBuilder(_cms);

			migrationSession.ExportSession.FileLocation = location;
			migrationSession.ExportSession.TargetFolder = topLevelId;
			migrationSession.ExportSession.IncludeContent = includeContent;
			migrationSession.ExportSession.IncludeLibrary = includeLibrary;
			migrationSession.ExportSession.IncludeModels = includeModels;
			migrationSession.ExportSession.IncludeTemplates = includeTemplates;
			migrationSession.ExportSession.ResourceCollection = resources;
			
			if (onItemProcessed != null) 
				migrationSession.ExportSession.ItemProcessed += onItemProcessed;
			
			migrationSession.Execute();

			migrationSession.EndSession();

			return migrationSession.ExportSession.Log;
		}

		#endregion

		#region Import Methods

		public IList<LogEntry> Import(string location, bool includeContent, bool includeLibrary, bool includeModels,
			bool includeTemplates, int targetFolder, IEnumerable<CmsResource> resources, bool overwriteExisting,
			Dictionary<string, int> accessMaps, Dictionary<string, int> packageMaps, Dictionary<string, int> stateMaps,
			Dictionary<string, int> workflowFilterMaps, EventHandler<MigrationItemEventArgs> onItemProcessed = null)
		{
			var migrationSession = new MigrationSessionBuilder(_cms, false);

			migrationSession.ImportSession.FileLocation = location;
			migrationSession.ImportSession.IncludeContent = includeContent;
			migrationSession.ImportSession.IncludeLibrary = includeLibrary;
			migrationSession.ImportSession.IncludeModels = includeModels;
			migrationSession.ImportSession.IncludeTemplates = includeTemplates;
			migrationSession.ImportSession.TargetFolder = targetFolder;
			migrationSession.ImportSession.ResourceCollection = resources;
			migrationSession.ImportSession.OverwriteExistingAssets = overwriteExisting;
			migrationSession.ImportSession.AccessMaps = accessMaps;
			migrationSession.ImportSession.PackageMaps = packageMaps;
			migrationSession.ImportSession.StateMaps = stateMaps;
			migrationSession.ImportSession.WorkflowFilterMaps = workflowFilterMaps;

			if (onItemProcessed != null) 
				migrationSession.ImportSession.ItemProcessed += onItemProcessed;

			migrationSession.Execute();

			migrationSession.EndSession();

			return migrationSession.ImportSession.Log;
		}

		public TreeNode[] GetTreeNodes(string importFile, bool includeContent, bool includeLibrary, bool includeModels, bool includeTemplates)
		{
			if (!System.IO.File.Exists(importFile)) throw new ArgumentException("File does not exist");

			var nodes = new List<TreeNode>();

			var xml = new XmlDocument();
			xml.Load(importFile);

			var topFolder = xml.SelectSingleNode("//folder[isTop='true']");
			TreeNode topNode, rootNode = null;
			var topPath = "/";
			if (topFolder == null)
			{
				// Make a node for the root folder
				topNode = new TreeNode("/")
				{
					Tag = new CmsResource
					{
						AssetId = 0,
						AssetType = CmsAssetType.Folder,
						Path = "/"
					},
					ImageIndex = Common.GetImageIndex(CmsAssetType.Folder),
				};
			}
			else
			{
				// Make a node for the top folder
				topPath = topFolder.SelectSingleNode("path").InnerText;
				topNode = new TreeNode(topFolder.SelectSingleNode("label").InnerText)
				{
					Tag = new CmsResource
					{
						AssetId = GetIntFromNode(topFolder.SelectSingleNode("id")).Value,
						AssetType = Common.GetAssetType(topFolder),
						Path = topPath
					},
					ImageIndex = Common.GetImageIndex(Common.GetAssetType(topFolder)),
				};
				// Make a second node for the root folder
				// It might get trimmed later
				rootNode = new TreeNode("/")
				{
					Tag = new CmsResource
					{
						AssetId = 0,
						AssetType = CmsAssetType.Folder,
						Path = "/"
					},
					ImageIndex = Common.GetImageIndex(CmsAssetType.Folder),
				};
			}
			topNode.SelectedImageIndex = topNode.ImageIndex;
			nodes.Add(topNode);
			if (rootNode != null)
				nodes.Add(rootNode);

			var assetsNode = xml.SelectSingleNode("/assets");
			if (assetsNode != null)
			{
				var assets = assetsNode.SelectNodes("*[not(isTop)]");
				if (assets != null)
				{
					foreach (XmlNode asset in assets)
					{
						var type = Common.GetAssetType(asset);
						// Exclude the types they don't want
						if (!includeLibrary && (type.HasFlag(CmsAssetType.LibraryFolder) || type.HasFlag(CmsAssetType.LibraryClass))) continue;
						if (!includeTemplates && (type.HasFlag(CmsAssetType.TemplatesFolder) || type.HasFlag(CmsAssetType.TemplateFolder) || type.HasFlag(CmsAssetType.Template))) continue;
						if (!includeModels && type.HasFlag(CmsAssetType.Model)) continue;
						if (!includeContent && (type.HasFlag(CmsAssetType.ContentAsset) || type.HasFlag(CmsAssetType.DigitalAsset))) continue;
						if (asset.Name == "folder" || asset.Name == "asset")
							CreateNodeForItem(asset, nodes, topPath);
					}
				}
			}

			// Removed 2019-10-01 as it kills empty folders that are useful in Models
			//PruneTree(topNode);

			if (rootNode != null && rootNode.Nodes.Count == 0)
			{
				// Kill the root node if we didn't need it
				nodes.RemoveAt(1);
				rootNode.Remove();
			}

			topNode.Expand();

			return nodes.ToArray();
		}

		/*
		// TODO: delete this once I'm sure it has no use any longer
		private void PruneTree(TreeNode node)
		{
			if (node == null) return;

			var children = new TreeNode[node.Nodes.Count];
			node.Nodes.CopyTo(children, 0);
			// Recurse first
			foreach (TreeNode child in children)
				PruneTree(child);

			// If it has no remaining children, but this type is allowed them, prune it
			if (node.Nodes.Count == 0 && Common.IsNodeAllowedChildren(node.ImageIndex))
				node.Remove();
		}
		*/

		private void CreateNodeForItem(XmlNode asset, List<TreeNode> nodes, string topPath)
		{
			var node = nodes[0];

			var pathNode = asset.SelectSingleNode("path");
			if (pathNode == null) throw new Exception("Path not found for asset");

			var path = pathNode.InnerText;

			if (!string.IsNullOrWhiteSpace(topPath) && path.StartsWith("/System") && nodes.Count > 1)
			{
				// Switch to the second node in the collection, which will be "/"
				node = nodes[1];
			}

			// If we're not exporting from the root, trim the path here
			if (path.StartsWith(topPath, StringComparison.OrdinalIgnoreCase))
				path = path.Substring(topPath.Length);

			var pathList = path.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
			var assetName = "";

			var assetType = Common.GetAssetType(asset);

			// We'll create the last item ourselves
			assetName = pathList.Last();
			pathList.RemoveAt(pathList.Count - 1);

			var workingNode = node;
			foreach (var pathSegment in pathList)
			{
				var found = false;
				foreach (TreeNode child in workingNode.Nodes)
				{
					if (child.Text == pathSegment)
					{
						workingNode = child;
						found = true;
						break;
					}
				}
				if (!found)
				{
					// Create a new node
					var newNode = new TreeNode(pathSegment);
					workingNode.Nodes.Add(newNode);
					workingNode = newNode;

					workingNode.ImageIndex = Common.GetImageIndex(CmsAssetType.Folder);
					workingNode.SelectedImageIndex = workingNode.ImageIndex;
					workingNode.Tag = null;

					// TODO: what if we find this folder later in the import?
				}
				// Make sure that the tag is up-to-date
				//workingNode.Tag = asset.SelectSingleNode("id").InnerText;
			}

			// Now we just create a final child for this item
			var assetNode = new TreeNode(assetName)
			{
				ImageIndex = Common.GetImageIndex(assetType),
				Tag = new CmsResource
				{
					AssetId = GetIntFromNode(asset.SelectSingleNode("id")).Value,
					AssetType = assetType,
					Name = assetName,
					Path = path
				}
			};
			assetNode.SelectedImageIndex = assetNode.ImageIndex;
			workingNode.Nodes.Add(assetNode);
		}

		public List<PreImportMessageGroup> FindProblemGroups(string importFile, TreeView tree)
		{
			var xml = new XmlDocument();
			xml.Load(importFile);

			var problems = new List<PreImportMessage>();
			
			foreach (TreeNode child in tree.Nodes)
			{
				problems.AddRange(FindProblems(xml, child));
			}

			return GroupProblems(problems);
		}

		private List<PreImportMessageGroup> GroupProblems(IEnumerable<PreImportMessage> problems)
		{
			var groups = new List<PreImportMessageGroup>();

			foreach (var problem in problems)
			{
				var group = groups.FirstOrDefault(g => g.Message == problem.Message);

				if (group == null)
				{
					// Create a new group
					group = new PreImportMessageGroup
					{
						Message = problem.Message,
						Status = problem.Status,
						AssetIds = new []{problem.AssetId},
						Type = problem.Type,
						Key = problem.Key
					};
					groups.Add(group);
				}
				else
				{
					// Append our asset id to this group
					var assets = group.AssetIds.ToList();
					assets.Add(problem.AssetId);
					group.AssetIds = assets.ToArray();
				}
			}

			return groups;
		}

		private List<PreImportMessage> FindProblems(XmlDocument xml, TreeNode node)
		{
			var result = new List<PreImportMessage>();

			var resource = node.Tag as CmsResource;
			// We don't need to look for the root node (or empty helper nodes we created)
			if ((node.Checked || node.HasCheckedDescendents()) && resource != null && resource.AssetId != 0)
			{
				var xmlNode = xml.SelectSingleNode("//*[id='" + resource.AssetId + "']");
				if (xmlNode == null)
				{
					result.Add(new PreImportMessage
					{
						AssetId = resource.AssetId,
						Message = "Cannot find asset",
						Status = MessageStatus.Error,
						Type = ProblemType.Other
					});
				}
				else
				{
					var pathNode = xmlNode.SelectSingleNode("path");
					var path = "";
					if (pathNode == null || string.IsNullOrWhiteSpace(pathNode.InnerText))
					{
						result.Add(new PreImportMessage
						{
							AssetId = resource.AssetId,
							Message = "Cannot find asset path",
							Status = MessageStatus.Error,
							Type = ProblemType.Other
						});
					}
					else
					{
						path = pathNode.InnerText;

						var modelPathNode = xmlNode.SelectSingleNode("model_path");
						if (modelPathNode != null)
						{
							var modelPath = modelPathNode.InnerText;
							if (!_modelMaps.ContainsKey(modelPath))
							{
								if (string.IsNullOrWhiteSpace(modelPath))
								{
									result.Add(new PreImportMessage
									{
										AssetId = resource.AssetId,
										Message = "No model provided",
										Path = path,
										Status = MessageStatus.Warning,
										Type = ProblemType.Model
									});
								}
								else
								{
									// First look in the import to see if the model is there and selected
									var modelNode = FindNodeById(node.TreeView, int.Parse(xmlNode.SelectSingleNode("model_id").InnerText));
									if (modelNode == null || !modelNode.Checked)
									{
										var modelAsset = _migrationEngine.GetAsset(modelPath);
										if (modelAsset == null)
										{
											result.Add(new PreImportMessage
											{
												AssetId = resource.AssetId,
												Message = "Model not found: " + modelPath,
												Path = path,
												Status = MessageStatus.Warning,
												Type = ProblemType.Model
											});
										}
										else
										{
											_modelMaps.Add(modelPath, modelAsset.id);
											resource.ModelId = _modelMaps[modelPath];
										}
									}
								}
							}
							else
							{
								resource.ModelId = _modelMaps[modelPath];
							}
						}

						var workflowIdNode = xmlNode.SelectSingleNode("workflow_id");
						if (workflowIdNode == null)
						{
							if (xmlNode.Name != "folder")
							{
								result.Add(new PreImportMessage
								{
									AssetId = resource.AssetId,
									Message = "No workflow provided",
									Path = path,
									Status = MessageStatus.Warning,
									Type = ProblemType.Workflow
								});
							}
						}
						else
						{
							var workflowNameNode = xmlNode.SelectSingleNode("workflow_name");
							if (workflowNameNode == null)
							{
								if (!string.IsNullOrEmpty(workflowIdNode.InnerText) && workflowIdNode.InnerText != "0")
								{
									result.Add(new PreImportMessage
									{
										AssetId = resource.AssetId,
										Message = "No workflow provided",
										Path = path,
										Status = MessageStatus.Warning,
										Type = ProblemType.Workflow
									});
								}
							}
							else
							{
								var workflowName = workflowNameNode.InnerText;
								if (!_workflowMaps.ContainsKey(workflowName))
								{
									if (!string.IsNullOrWhiteSpace(workflowName))
									{
										// First look in the import to see if the workflow is there and selected
										TreeNode workflowNode = null;

										var workflowAssetNode = xmlNode.SelectSingleNode("/*/asset[workflow/id=\"" + workflowIdNode.InnerText + "\"]/id");
										if (workflowAssetNode != null)
										{
											workflowNode = FindNodeById(node.TreeView, int.Parse(workflowAssetNode.InnerText));
										}

										if (workflowNode == null || !workflowNode.Checked)
										{
											var workflow = _migrationEngine.GetWorkflow(workflowName);
											if (workflow == null)
											{
												result.Add(new PreImportMessage
												{
													AssetId = resource.AssetId,
													Message = "Workflow not found: " + workflowName,
													Path = path,
													Status = MessageStatus.Warning,
													Type = ProblemType.Workflow
												});
											}
											else if (_migrationEngine.IsWorkflowDuplicateName(workflowName))
											{
												result.Add(new PreImportMessage
												{
													AssetId = resource.AssetId,
													Message = "Duplicate workflow name found: " + workflowName,
													Path = path,
													Status = MessageStatus.Warning,
													Type = ProblemType.Workflow
												});
											}
											else
											{
												_workflowMaps.Add(workflowName, workflow.Id);
												resource.WorkflowId = workflow.Id;
											}
										}
									}
								}
								else
								{
									resource.WorkflowId = _workflowMaps[workflowName];
								}
							}
						}

						var templatePathNode = xmlNode.SelectSingleNode("template_path");
						if (templatePathNode == null)
						{
							// Don't report no template for folders or workflows or digital assets
							if (xmlNode.Name == "asset" && xmlNode.SelectSingleNode("binaryContent") == null 
							                            && xmlNode.SelectSingleNode("workflow") == null)
							{
								result.Add(new PreImportMessage
								{
									AssetId = resource.AssetId,
									Message = "No template provided",
									Path = path,
									Status = MessageStatus.Warning,
									Type = ProblemType.Template
								});
							}
						}
						else
						{
							var templatePath = templatePathNode.InnerText;
							if (!_templateMaps.ContainsKey(templatePath))
							{
								if (string.IsNullOrWhiteSpace(templatePath))
								{
									result.Add(new PreImportMessage
									{
										AssetId = resource.AssetId,
										Message = "No template provided",
										Path = path,
										Status = MessageStatus.Warning,
										Type = ProblemType.Template
									});
								}
								else
								{
									// First look in the import to see if the template is there and selected
									var templateNode = FindNodeById(node.TreeView, int.Parse(xmlNode.SelectSingleNode("template_id").InnerText));
									if (templateNode == null || !templateNode.Checked)
									{
										var templateAsset = _migrationEngine.GetAsset(templatePath);
										if (templateAsset == null)
										{
											result.Add(new PreImportMessage
											{
												AssetId = resource.AssetId,
												Message = "Template not found: " + templatePath,
												Path = path,
												Status = MessageStatus.Warning,
												Type = ProblemType.Template
											});
										}
										else
										{
											_templateMaps.Add(templatePath, templateAsset.id);
											resource.TemplateId = _templateMaps[templatePath];
										}
									}
								}
							}
							else
							{
								resource.TemplateId = _templateMaps[templatePath];
							}
						}

						// TODO: check for branch id and that we're importing the asset with that branch id

						if (xmlNode.SelectSingleNode("publishing_properties/property/package") != null)
						{
							foreach (var packageNode in xmlNode.SelectNodes("publishing_properties/property/package"))
							{
								var packageName = ((XmlNode)packageNode).InnerText;
								if (!_packageMaps.ContainsKey(packageName))
								{
									var package = _migrationEngine.GetPublishingPackage(packageName);
									if (package == null)
									{
										result.Add(new PreImportMessage
										{
											AssetId = resource.AssetId,
											Message = "Package not found: " + packageName,
											Path = path,
											Status = MessageStatus.Warning,
											Type = ProblemType.Package,
											Key = packageName
										});
									}
									else
									{
										_packageMaps.Add(packageName, package.Id);
									}
								}
							}
						}

						if (xmlNode.SelectSingleNode("workflow") != null)
						{
							// This is a workflow item
							var workflowNode = xmlNode.SelectSingleNode("workflow");

							// First check step/accessFile nodes
							foreach (var accessFileNode in workflowNode.SelectNodes("steps/step/accessFile"))
							{
								var accessPath = ((XmlNode) accessFileNode).InnerText;
								if (!_accessMaps.ContainsKey(accessPath))
								{
									var accessAsset = _migrationEngine.GetAsset(accessPath);
									if (accessAsset == null)
									{
										result.Add(new PreImportMessage
										{
											AssetId = resource.AssetId,
											Message = "Access asset not found: " + accessPath,
											Path = path,
											Status = MessageStatus.Warning,
											Type = ProblemType.Access,
											Key = accessPath
										});
									}
									else
									{
										_accessMaps.Add(accessPath, accessAsset.id);
									}
								}
							}

							// Next check step/statusName
							foreach (var statusNameNode in workflowNode.SelectNodes("steps/step/statusName"))
							{
								var statusName = ((XmlNode)statusNameNode).InnerText;
								if (!_stateMaps.ContainsKey(statusName))
								{
									var statusAsset = _migrationEngine.GetAsset("/System/States/" + statusName);
									if (statusAsset == null)
									{
										result.Add(new PreImportMessage
										{
											AssetId = resource.AssetId,
											Message = "State not found: " + statusName,
											Path = path,
											Status = MessageStatus.Warning,
											Type = ProblemType.State,
											Key = statusName
										});
									}
									else
									{
										_stateMaps.Add(statusName, statusAsset.id);
									}
								}
							}

							// Next check step/commands/command/filterName
							foreach (var filterNameNode in workflowNode.SelectNodes("steps/step/commands/command/filterName"))
							{
								var filterName = ((XmlNode) filterNameNode).InnerText;
								if (!_workflowFilterMaps.ContainsKey(filterName))
								{
									var workflowFilter = _migrationEngine.GetWorkflowFilter(filterName);
									if (workflowFilter == null)
									{
										result.Add(new PreImportMessage
										{
											AssetId = resource.AssetId,
											Message = "Workflow filter not found: " + filterName,
											Path = path,
											Status = MessageStatus.Warning,
											Type = ProblemType.WorkflowFilter,
											Key = filterName
										});
									}
									else
									{
										_workflowFilterMaps.Add(filterName, workflowFilter.Id);
									}
								}
							}

							// Next check step/publishes/publish/statusName
							foreach (var statusNameNode in workflowNode.SelectNodes("steps/step/publishes/publish/statusName"))
							{
								var statusName = ((XmlNode)statusNameNode).InnerText;
								if (!_stateMaps.ContainsKey(statusName))
								{
									var statusAsset = _migrationEngine.GetAsset("/System/States/" + statusName);
									if (statusAsset == null)
									{
										result.Add(new PreImportMessage
										{
											AssetId = resource.AssetId,
											Message = "State not found: " + statusName,
											Path = path,
											Status = MessageStatus.Warning,
											Type = ProblemType.State,
											Key = statusName
										});
									}
									else
									{
										_stateMaps.Add(statusName, statusAsset.id);
									}
								}
							}

							// Next check step/publishes/publish/packageName
							foreach (var packageNameNode in workflowNode.SelectNodes("steps/step/publishes/publish/packageName"))
							{
								var packageName = ((XmlNode)packageNameNode).InnerText;
								if (!_packageMaps.ContainsKey(packageName))
								{
									var package = _migrationEngine.GetPublishingPackage(packageName);
									if (package == null)
									{
										result.Add(new PreImportMessage
										{
											AssetId = resource.AssetId,
											Message = "Package not found: " + packageName,
											Path = path,
											Status = MessageStatus.Warning,
											Type = ProblemType.Package,
											Key = packageName
										});
									}
									else
									{
										_packageMaps.Add(packageName, package.Id);
									}
								}
							}
						}
					}
				}
				// Make sure the tag is up-to-date
				node.Tag = resource;
			}

			// Recurse to our children
			foreach (TreeNode child in node.Nodes)
			{
				result.AddRange(FindProblems(xml, child));
			}

			return result;
		}

		private int? GetIntFromNode(XmlNode node)
		{
			if (node == null) return null;
			if (int.TryParse(node.InnerText, out var result)) return result;
			return null;
		}

		private TreeNode FindNodeById(TreeView tree, int id)
		{
			foreach (TreeNode child in tree.Nodes)
			{
				var found = FindNodeById(child, id);
				if (found != null) return found;
			}
			return null;
		}

		private TreeNode FindNodeById(TreeNode node, int id)
		{
			if (((CmsResource)node.Tag).AssetId == id) return node;

			foreach (TreeNode child in node.Nodes)
			{
				var found = FindNodeById(child, id);
				if (found != null) return found;
			}
			return null;
		}

		public void GetMaps(out Dictionary<string, int> accessMaps, out Dictionary<string, int> packageMaps,
			out Dictionary<string, int> stateMaps, out Dictionary<string, int> workflowFilterMaps)
		{
			accessMaps = _accessMaps;
			packageMaps = _packageMaps;
			stateMaps = _stateMaps;
			workflowFilterMaps = _workflowFilterMaps;
		}

		#endregion

		#region Shared Methods

		public static bool Authenticate(string server, string instance, string developerKey, string username, string password, bool useWco, string wcoUsername, string wcoPassword)
		{
			var result = Authenticate(new CmsInstance
			{
				Server = server,
				Instance = instance,
				Key = developerKey,
				Username = username,
				Password = password
			});
			if (useWco)
			{
				result = result && WcoAuthenticate(new CmsInstance
				{
					WcoUsername = wcoUsername,
					WcoPassword = wcoPassword
				});
			}
			return result;
		}

		public static bool Authenticate(CmsInstance cms)
		{
			try
			{
				return MigrationEngine.Authenticate(cms);
			}
			catch
			{
				return false;
			}
		}

		public static bool WcoAuthenticate(CmsInstance cms)
		{
			try
			{
				return MigrationEngine.WcoAuthenticate(cms);
			}
			catch
			{
				return false;
			}
		}

		public IEnumerable<CmsResource> GetCmsResources(TreeView tree)
		{
			var resources = new List<CmsResource>();

			foreach (TreeNode node in tree.Nodes)
			{
				GetCmsResources(node, ref resources, "");
			}

			return resources;
		}

		private void GetCmsResources(TreeNode node, ref List<CmsResource> resources, string path)
		{
			if (node.Text.StartsWith("/") || path.EndsWith("/"))
				path = string.Concat(path, node.Text);
			else
				path = string.Concat(path, "/", node.Text);

			if (node.Tag != null)
			{
				var id = (node.Tag is CmsResource)
					? ((CmsResource) node.Tag).AssetId
					: Convert.ToInt32(node.Tag.ToString());

				if (id != 0 && (node.Checked || node.HasCheckedDescendents()))
				{
					if (node.Tag is CmsResource)
					{
						resources.Add((CmsResource) node.Tag);
					}
					else
					{
						resources.Add(new CmsResource {AssetId = id, Name = node.Text, Path = path});
					}
				}
			}
			foreach (TreeNode child in node.Nodes)
			{
				GetCmsResources(child, ref resources, path);
			}
		}

		public static string GetLogAsText(IList<LogEntry> log)
		{
			if (log == null) return "";

			var result = new StringBuilder(10240);
			foreach (var entry in log)
			{
				result.AppendLine(entry.ToString());
			}
			return result.ToString();
		}

		public WorklistAsset GetAsset(int id, bool ensurePath = false)
		{
			return _migrationEngine.GetAsset(id, ensurePath);
		}

		public WorklistAsset GetAsset(string path)
		{
			return _migrationEngine.GetAsset(path);
		}

		public IList<CmsResource> GetAssetList(int id)
		{
			return _migrationEngine.GetAssetList(id);
		}

		public IList<CmsResource> GetAssetList(string path)
		{
			return _migrationEngine.GetAssetList(path);
		}

		public WorkflowData GetWorkflow(int id)
		{
			return _migrationEngine.GetWorkflow(id);
		}

		public WorkflowData GetWorkflow(string name)
		{
			return _migrationEngine.GetWorkflow(name);
		}

		public Dictionary<int, WorkflowData> GetWorkflows()
		{
			return _migrationEngine.GetWorkflows();
		}

		public WorkflowFilter GetWorkflowFilter(int id)
		{
			return _migrationEngine.GetWorkflowFilter(id);
		}

		public WorkflowFilter GetWorkflowFilter(string name)
		{
			return _migrationEngine.GetWorkflowFilter(name);
		}

		public Dictionary<int, WorkflowFilter> GetWorkflowFilters()
		{
			return _migrationEngine.GetWorkflowFilters();
		}

		public PublishingPackage GetPublishingPackage(int id)
		{
			return _migrationEngine.GetPublishingPackage(id);
		}

		public PublishingPackage GetPublishingPackage(string name)
		{
			return _migrationEngine.GetPublishingPackage(name);
		}

		public Dictionary<int, PublishingPackage> GetPublishingPackages()
		{
			return _migrationEngine.GetPublishingPackages();
		}

		public void PopulateAssetFullPath(WorklistAsset asset)
		{
			if (asset.id == 0) asset.FullPath = "/";
			else if (string.IsNullOrWhiteSpace(asset.FullPath))
			{
				var path = "";
				var workingAsset = asset;
				while (workingAsset.folder_id.HasValue && workingAsset.folder_id != 1)
				{
					// This is the root - we can stop now
					if (workingAsset.id == 0) break;

					if (!string.IsNullOrWhiteSpace(workingAsset.FullPath))
					{
						// If the full path is available here, we can use it and jump out
						path = string.Concat(workingAsset.FullPath, path);
						break;
					}

					path = string.Concat("/", workingAsset.label, path);
					workingAsset = GetAsset(workingAsset.folder_id.Value);
				}
				asset.FullPath = path;
			}
		}

		public static bool IsSite(WorklistAsset asset)
		{
			return asset.subtype.HasValue && (asset.subtype.Value & SITE) > 0;
		}

		#endregion
	}
}
