using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crownpeak.ContentXcelerator.Migrator;
using Crownpeak.ContentXcelerator.Migrator.UI.Properties;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	class AppSettings
	{
		private static object GetProperty(string name)
		{
			return Settings.Default[name];
		}

		private static void SetProperty(string name, object value)
		{
			Settings.Default[name] = value;
		}

		public static CmsInstance[] GetConnections(SettingsType type)
		{
			var name = "sessions" + (type == SettingsType.ForImport ? "Import" : "Export");
			return DeserializeCmsInstances((string)Settings.Default[name]);
		}

		public static CmsInstance GetLastConnection(SettingsType type)
		{
			var name = "lastsession" + (type == SettingsType.ForImport ? "Import" : "Export");
			return DeserializeCmsInstance((string)Settings.Default[name]);
		}

		public static void SaveConnection(CmsInstance value, SettingsType type)
		{
			// Save as the last session
			var name = "lastsession" + (type == SettingsType.ForImport ? "Import" : "Export");
			SetProperty(name, SerializeCmsInstance(value));

			var found = false;
			var sessions = GetConnections(type);
			foreach (var session in sessions)
			{
				if (session.Server == value.Server && session.Instance == value.Instance)
				{
					session.Key = value.Key;
					session.Username = value.Username;
					SaveConnections(sessions, type);
					found = true;
				}
			}
			if (!found)
			{
				// Not found, so append our item
				var list = sessions.ToList();
				list.Add(value);
				SaveConnections(list.ToArray(), type);
			}

			Settings.Default.Save();
		}

		private static void SaveConnections(CmsInstance[] values, SettingsType type)
		{
			var name = "sessions" + (type == SettingsType.ForImport ? "Import" : "Export");
			SetProperty(name, SerializeCmsInstances(values));
		}

		public static void ClearConnections(SettingsType type)
		{
			SaveConnections(new CmsInstance[0], type);
		}

		private static string SerializeCmsInstance(CmsInstance value)
		{
			return value.Server + "," + value.Instance + "," + value.Key + "," + value.Username + "," + value.WcoUsername;
		}

		private static CmsInstance DeserializeCmsInstance(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return null;

			var temp = value.Split(",".ToCharArray());
			var cmsInstance = new CmsInstance
			{
				Server = temp[0],
				Instance = temp[1],
				Key = temp[2],
				Username = temp[3]
			};
			if (temp.Length > 4 && !string.IsNullOrWhiteSpace(temp[4]))
				cmsInstance.WcoUsername = temp[4];

			return cmsInstance;
		}

		private static string SerializeCmsInstances(CmsInstance[] values)
		{
			return string.Join("|", values.Select(SerializeCmsInstance));
		}

		private static CmsInstance[] DeserializeCmsInstances(string values)
		{
			if (string.IsNullOrWhiteSpace(values)) return new CmsInstance[0];
			return values.Split("|".ToCharArray()).Select(DeserializeCmsInstance).ToArray();
		}
	}

	public enum SettingsType
	{
		ForImport,
		ForExport
	}
}
