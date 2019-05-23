using System;
using System.Collections.Generic;
using System.Linq;
using CrownPeak.AccessApiHelper;
using CrownPeak.AccessApiHelper.ApiAccessor;
using CrownPeak.AccessAPI;

namespace Crownpeak.ContentXcelerator.Migrator.UI
{
	class CmsWrapper
	{
		private const uint PROJECT = 32,
			LIBRARY = 64,
			SITE = 128,
			TEMPLATES = 256,
			TEMPLATE = 512,
			WORKFLOWS = 1024,
			STATES = 2048;
		private CmsApi _cms = new CmsApi(new SimpleApiAccessor());

		public bool IsInitialized { get; private set; }
		public bool IsLoggedIn { get; private set; }

		public CmsWrapper()
		{
			Reset();
		}

		public void Init(string server, string instance, string developerKey)
		{
			IsLoggedIn = false;
			_cms.Init(server, instance, developerKey);
			IsInitialized = true;
		}

		public bool Login(string username, string password)
		{
			if (!IsInitialized) throw new Exception("You must initialize before logging in");
			IsLoggedIn = _cms.Login(username, password);
			return IsLoggedIn;
		}

		public void Reset()
		{
			IsInitialized = false;
			IsLoggedIn = false;
		}

		public bool EnsureConnection(string server, string instance, string developerKey, string username, string password)
		{
			try
			{
				if (!IsInitialized) Init(server, instance, developerKey);
				if (!IsLoggedIn) return Login(username, password);

				return true;
			}
			catch (Exception)
			{
				IsInitialized = false;
				IsLoggedIn = false;
				return false;
			}
		}

		public WorklistAsset GetAsset(int id)
		{
			if (!IsLoggedIn) throw new Exception("You must log in before accessing assets");

			WorklistAsset asset;
			if (_cms.Asset.Read(id, out asset))
				return asset;

			return null;
		}

		public WorklistAsset GetAsset(string idOrPath)
		{
			if (!IsLoggedIn) throw new Exception("You must log in before accessing assets");

			int id;
			if (_cms.Asset.Exists(idOrPath, out id))
			{
				WorklistAsset asset;
				if (_cms.Asset.Read(id, out asset))
					return asset;
			}
			return null;
		}

		public WorklistAsset[] GetAssets(int folderId)
		{
			if (!IsLoggedIn) throw new Exception("You must log in before accessing assets");
			int normalCount, hiddenCount, deletedCount;
			IEnumerable<WorklistAsset> contents;

			if (_cms.Asset.GetList(folderId, 0, 9999, "Label", OrderType.Ascending, VisibilityType.Normal, false, false,
				out contents, out normalCount, out hiddenCount, out deletedCount))
				return contents.ToArray();

			return new WorklistAsset[0];
		}

		public bool IsSite(WorklistAsset asset)
		{
			return asset.subtype.HasValue && (asset.subtype.Value & SITE) > 0;
		}
	}
}
