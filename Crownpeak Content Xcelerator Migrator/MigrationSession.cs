using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class MigrationSession : IMigrationSession
	{
		public event EventHandler<MigrationItemEventArgs> ItemProcessed;
		public bool IncludeLibrary { get; set; }
		public bool IncludeTemplates { get; set; }
		public bool IncludeModels { get; set; }
		public bool IncludeContent { get; set; }

		public IList<LogEntry> Log { get; set; }
		public int TargetFolder { get; set; }
		public CmsInstance Instance { get; set; }
		public string FileLocation { get; set; }


		public void ItemProcessedEvent(LogEntry logEntry, int count, int total)
		{
			ItemProcessed?.Invoke(null, new MigrationItemEventArgs { LogEntry = logEntry, Count = count, Total = total });
		}

		public MigrationSession()
		{
			IncludeLibrary = true;
			IncludeContent = true;
			IncludeModels = true;
			IncludeTemplates = true;
		}

		public void LogEntry(string resource, string message, EventLogEntryType eventLogEntryType)
		{

			if (Log == null)
				Log = new List<LogEntry>();

			var logEntry = new LogEntry
			{
				EntryTime = DateTime.Now,
				EntryType = eventLogEntryType,
				Message = message,
				Resource = resource
			};

			Log.Add(logEntry);
			ItemProcessedEvent(logEntry, 0, 0);
		}

		public void LogEntry(string resource, string message, EventLogEntryType eventLogEntryType, int count, int total)
		{

			if (Log == null)
				Log = new List<LogEntry>();

			var logEntry = new LogEntry
			{
				EntryTime = DateTime.Now,
				EntryType = eventLogEntryType,
				Message = message,
				Resource = resource
			};

			Log.Add(logEntry);
			ItemProcessedEvent(logEntry, count, total);
		}
	}
}