using System;
using System.Diagnostics;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class LogEntry
	{
		public DateTime EntryTime { get; set; }
		public string Resource { get; set; }
		public string Message { get; set; }
		public EventLogEntryType EntryType { get; set; }

		public override string ToString()
		{
			return $"{EntryTime:yyyy-MM-dd HH:mm:ss.fff} {EntryType} {Message} {Resource}";
		}
	}
}