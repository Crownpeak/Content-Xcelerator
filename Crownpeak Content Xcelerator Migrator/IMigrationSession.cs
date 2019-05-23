using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public interface IMigrationSession
	{
		event EventHandler<MigrationItemEventArgs> ItemProcessed;
		bool IncludeLibrary { get; set; }
		bool IncludeTemplates { get; set; }
		bool IncludeModels { get; set; }
		bool IncludeContent { get; set; }
		IList<LogEntry> Log { get; set; }
		int TargetFolder { get; set; }
		CmsInstance Instance { get; set; }
		void LogEntry(string resource, string message, EventLogEntryType eventLogEntryType, int count, int total);
		void LogEntry(string resource, string message, EventLogEntryType eventLogEntryType);
		void ItemProcessedEvent(LogEntry logEntry, int count, int total);
	}
}
