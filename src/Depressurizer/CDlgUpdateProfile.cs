﻿/*
This file is part of Depressurizer.
Copyright 2011, 2012, 2013 Steve Labbe.

Depressurizer is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Depressurizer is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Depressurizer.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Xml;
using Depressurizer.Core;
using Depressurizer.Core.Enums;
using Rallion;

namespace Depressurizer
{
	internal class CDlgUpdateProfile : CancelableDlg
	{
		#region Fields

		private readonly bool custom;

		private readonly string customUrl;

		private readonly GameList data;

		private readonly SortedSet<int> ignore;

		private readonly bool overwrite;

		private readonly long SteamId;

		private XmlDocument doc;

		private string htmlDoc;

		#endregion

		#region Constructors and Destructors

		public CDlgUpdateProfile(GameList data, long accountId, bool overwrite, SortedSet<int> ignore) : base(GlobalStrings.CDlgUpdateProfile_UpdatingGameList, true)
		{
			custom = false;
			SteamId = accountId;

			Added = 0;
			Fetched = 0;
			UseHtml = false;
			Failover = false;

			this.data = data;

			this.overwrite = overwrite;
			this.ignore = ignore;

			SetText(GlobalStrings.CDlgFetch_DownloadingGameList);
		}

		public CDlgUpdateProfile(GameList data, string customUrl, bool overwrite, SortedSet<int> ignore) : base(GlobalStrings.CDlgUpdateProfile_UpdatingGameList, true)
		{
			custom = true;
			this.customUrl = customUrl;

			Added = 0;
			Fetched = 0;
			UseHtml = false;
			Failover = false;

			this.data = data;

			this.overwrite = overwrite;
			this.ignore = ignore;

			SetText(GlobalStrings.CDlgFetch_DownloadingGameList);
		}

		#endregion

		#region Public Properties

		public int Added { get; private set; }

		public bool Failover { get; private set; }

		public int Fetched { get; private set; }

		public int Removed { get; private set; }

		public bool UseHtml { get; private set; }

		#endregion

		#region Methods

		protected void FetchHtml()
		{
			UseHtml = true;
			htmlDoc = custom ? GameList.FetchHtmlGameList(customUrl) : GameList.FetchHtmlGameList(SteamId);
		}

		protected void FetchXml()
		{
			UseHtml = false;
			doc = custom ? GameList.FetchXmlGameList(customUrl) : GameList.FetchXmlGameList(SteamId);
		}

		protected void FetchXmlPref()
		{
			try
			{
				FetchXml();

				return;
			}
			catch (Exception)
			{
			}

			Failover = true;
			FetchHtml();
		}

		protected override void Finish()
		{
			if (Canceled || (Error != null) || (UseHtml ? htmlDoc == null : doc == null))
			{
				return;
			}

			SetText(GlobalStrings.CDlgFetch_FinishingDownload);
			if (UseHtml)
			{
				Fetched = data.IntegrateHtmlGameList(htmlDoc, overwrite, ignore, out int newItems);
				Added = newItems;
			}
			else
			{
				Fetched = data.IntegrateXmlGameList(doc, overwrite, ignore, out int newItems);
				Added = newItems;
			}

			OnJobCompletion();
		}

		protected override void RunProcess()
		{
			Added = 0;
			Fetched = 0;
			switch (Settings.Instance.ListSource)
			{
				case GameListSource.XmlPreferred:
					FetchXmlPref();

					break;
				case GameListSource.XmlOnly:
					FetchXml();

					break;
				case GameListSource.WebsiteOnly:
					FetchHtml();

					break;
			}

			OnThreadCompletion();
		}

		#endregion
	}
}
