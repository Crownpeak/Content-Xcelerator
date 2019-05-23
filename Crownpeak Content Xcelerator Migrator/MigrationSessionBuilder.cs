using System.Collections.Generic;
using System.Diagnostics;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class MigrationSessionBuilder
	{
		private readonly bool _isExport;
		private readonly IMigrationSession _migrationSession;
		private readonly MigrationEngine _migrationEngine;

		public ExportSession ExportSession => (ExportSession)_migrationSession;

		public ImportSession ImportSession => (ImportSession)_migrationSession;

		public MigrationSessionBuilder(CmsInstance cmsInstance, bool isExport = true)
		{
			_isExport = isExport;
			if (_isExport)
			{
				_migrationSession = new ExportSession();
			}
			else
			{
				_migrationSession = new ImportSession();
			}

			_migrationSession.Instance = cmsInstance;
			_migrationSession.Log = new List<LogEntry>();
			_migrationEngine = new MigrationEngine(cmsInstance);
			_migrationSession.LogEntry("", "CMS Connection initalised and successfully authenticated. User: " + _migrationSession.Instance.Username, EventLogEntryType.Information);

		}

		public IList<CmsResource> GetFolder(int assetId)
		{
			return _migrationEngine.GetAssetList(assetId);
		}

		public IList<CmsResource> GetFolder(string path)
		{
			return _migrationEngine.GetAssetList(path);
		}

		public void Execute()
		{
			_migrationSession.LogEntry("", "Starting " + (_isExport ? "Export" : "Import") + " Session", EventLogEntryType.Information);

			if (_isExport)
			{
				_migrationEngine.Export(ExportSession);
			}

			else
			{
				_migrationEngine.Import(ImportSession);
			}
		}

		public void EndSession()
		{
			_migrationSession.LogEntry("", "Ending " + (_isExport ? "Export" : "Import") + " Session", EventLogEntryType.Information);
			_migrationEngine.EndSession();
		}
	}
}
