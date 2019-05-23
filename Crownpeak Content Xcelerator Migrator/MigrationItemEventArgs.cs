using System;

namespace Crownpeak.ContentXcelerator.Migrator
{
	public class MigrationItemEventArgs : EventArgs
	{
		public LogEntry LogEntry { get; set; }
		public int Count { get; set; }
		public int Total { get; set; }
	}
}