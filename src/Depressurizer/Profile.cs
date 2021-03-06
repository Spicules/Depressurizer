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
using System.Drawing;
using System.Net.Cache;
using System.Xml;
using Depressurizer.Core;
using Depressurizer.Core.Enums;
using Depressurizer.Core.Helpers;
using Depressurizer.Core.Models;
using Depressurizer.Models;
using Depressurizer.Properties;

namespace Depressurizer
{
	public class Profile
	{
		#region Constants

		public const int VERSION = 3;

		private const string XmlName_AutoCatList = "autocats";

		private const string XmlName_AutoExport = "auto_export";

		private const string XmlName_AutoIgnore = "auto_ignore";

		private const string XmlName_AutoImport = "auto_import";

		private const string XmlName_AutoUpdate = "auto_update";

		private const string XmlName_BypassIgnoreOnImport = "bypass_ignore_on_import";

		private const string XmlName_Exclusion = "exclusion";

		private const string XmlName_ExclusionList = "exclusions";

		private const string XmlName_ExportDiscard = "export_discard";

		private const string XmlName_Filter = "Filter";

		private const string XmlName_FilterAllow = "Allow";

		private const string XmlName_FilterExclude = "Exclude";

		private const string XmlName_FilterGame = "Game";

		private const string XmlName_FilterHidden = "Hidden";

		private const string XmlName_FilterList = "Filters";

		private const string XmlName_FilterName = "Name";

		private const string XmlName_FilterRequire = "Require";

		private const string XmlName_FilterSoftware = "Software";

		private const string XmlName_FilterUncategorized = "Uncategorized";

		private const string XmlName_FilterVR = "VR";

		private const string XmlName_Game = "game";

		private const string XmlName_Game_Category = "category";

		private const string XmlName_Game_CategoryList = "categories";

		private const string XmlName_Game_Executable = "executable";

		private const string XmlName_Game_Hidden = "hidden";

		private const string XmlName_Game_Id = "id";

		private const string XmlName_Game_LastPlayed = "lastplayed";

		private const string XmlName_Game_Name = "name";

		private const string XmlName_Game_Source = "source";

		private const string XmlName_GameList = "games";

		private const string XmlName_IncludeShortcuts = "include_shortcuts";

		private const string XmlName_IncludeUnknown = "include_unknown";

		private const string XmlName_LocalUpdate = "local_update";

		private const string XmlName_Old_AutoDownload = "auto_download";

		private const string XmlName_Old_Game_Favorite = "favorite";

		private const string XmlName_Old_IgnoreExternal = "ignore_external";

		// Old Xml names
		private const string XmlName_Old_SteamIDShort = "account_id";

		private const string XmlName_OverwriteNames = "overwrite_names";

		private const string XmlName_Profile = "profile";

		private const string XmlName_SteamID = "steam_id_64";

		private const string XmlName_Version = "version";

		private const string XmlName_WebUpdate = "web_update";

		#endregion

		#region Fields

		public List<AutoCat> AutoCats = new List<AutoCat>();

		public bool AutoExport = true;

		public bool AutoIgnore = true;

		public bool AutoImport = true;

		public bool AutoUpdate = true;

		public bool BypassIgnoreOnImport;

		public bool ExportDiscard = true;

		public string FilePath;

		public GameList GameData = new GameList();

		public SortedSet<int> IgnoreList = new SortedSet<int>();

		public bool IncludeShortcuts = true;

		public bool IncludeUnknown;

		public bool LocalUpdate = true;

		public bool OverwriteOnDownload = false;

		public long SteamID64;

		public bool WebUpdate = true;

		#endregion

		#region Properties

		private static Database Database => Database.Instance;

		private static Logger Logger => Logger.Instance;

		#endregion

		#region Public Methods and Operators

		public static long DirNametoID64(string cId)
		{
			if (long.TryParse(cId, out long res))
			{
				return res + 0x0110000100000000;
			}

			return 0;
		}

		/// <summary>
		///     Generates default autocats and adds them to a list
		/// </summary>
		/// <param name="list">The list where the autocats should be added</param>
		public static void GenerateDefaultAutoCatSet(List<AutoCat> list)
		{
			//By Genre
			list.Add(new AutoCatGenre(GlobalStrings.Profile_DefaultAutoCatName_Genre, null, "(" + GlobalStrings.Name_Genre + ") "));

			//By Year
			list.Add(new AutoCatYear(GlobalStrings.Profile_DefaultAutoCatName_Year, null, "(" + GlobalStrings.Name_Year + ") "));

			//By Score
			AutoCatUserScore ac = new AutoCatUserScore(GlobalStrings.Profile_DefaultAutoCatName_UserScore, null, "(" + GlobalStrings.Name_Score + ") ");
			ac.GenerateSteamRules(ac.Rules);
			list.Add(ac);

			//By Tags
			AutoCatTags act = new AutoCatTags(GlobalStrings.Profile_DefaultAutoCatName_Tags, null, "(" + GlobalStrings.Name_Tags + ") ");
			foreach (Tuple<string, float> tag in Database.CalculateSortedTagList(null, 1, 20, 0, false, false))
			{
				act.IncludedTags.Add(tag.Item1);
			}

			list.Add(act);

			//By Flags
			AutoCatFlags acf = new AutoCatFlags(GlobalStrings.Profile_DefaultAutoCatName_Flags, null, "(" + GlobalStrings.Name_Flags + ") ");
			foreach (string flag in Database.GetAllStoreFlags())
			{
				acf.IncludedFlags.Add(flag);
			}

			list.Add(acf);

			//By HLTB
			AutoCatHltb ach = new AutoCatHltb(GlobalStrings.Profile_DefaultAutoCatName_Hltb, null, "(HLTB) ", false);
			ach.Rules.Add(new Hltb_Rule(" 0-5", 0, 5, TimeType.Extras));
			ach.Rules.Add(new Hltb_Rule(" 5-10", 5, 10, TimeType.Extras));
			ach.Rules.Add(new Hltb_Rule("10-20", 10, 20, TimeType.Extras));
			ach.Rules.Add(new Hltb_Rule("20-50", 20, 50, TimeType.Extras));
			ach.Rules.Add(new Hltb_Rule("50+", 20, 0, TimeType.Extras));
			list.Add(ach);

			//By Platform
			AutoCatPlatform acPlatform = new AutoCatPlatform(GlobalStrings.Profile_DefaultAutoCatName_Platform, null, "(" + GlobalStrings.AutoCat_Name_Platform + ") ", true, true, true, true);
			list.Add(acPlatform);
		}

		public static string ID64toDirName(long id)
		{
			return (id - 0x0110000100000000).ToString();
		}

		public static Profile Load(string path)
		{
			Logger.Info(GlobalStrings.Profile_LoadingProfile, path);
			Profile profile = new Profile
			{
				FilePath = path
			};

			XmlDocument doc = new XmlDocument();

			try
			{
				doc.Load(path);
			}
			catch (Exception e)
			{
				Logger.Warn(GlobalStrings.Profile_FailedToLoadProfile, e.Message);

				throw new ApplicationException(GlobalStrings.Profile_ErrorLoadingProfile + e.Message, e);
			}

			XmlNode profileNode = doc.SelectSingleNode("/" + XmlName_Profile);

			if (profileNode != null)
			{
				// Get the profile version that we're loading
				XmlAttribute versionAttr = profileNode.Attributes[XmlName_Version];
				int profileVersion = 0;
				if (versionAttr != null)
				{
					if (!int.TryParse(versionAttr.Value, out profileVersion))
					{
						profileVersion = 0;
					}
				}

				// Get the 64-bit Steam ID
				long accId = XmlUtil.GetInt64FromNode(profileNode[XmlName_SteamID], 0);
				if (accId == 0)
				{
					string oldAcc = XmlUtil.GetStringFromNode(profileNode[XmlName_Old_SteamIDShort], null);
					if (oldAcc != null)
					{
						accId = DirNametoID64(oldAcc);
					}
				}

				profile.SteamID64 = accId;

				// Get other attributes
				profile.AutoUpdate = XmlUtil.GetBoolFromNode(profileVersion < 3 ? profileNode[XmlName_Old_AutoDownload] : profileNode[XmlName_AutoUpdate], profile.AutoUpdate);

				profile.AutoImport = XmlUtil.GetBoolFromNode(profileNode[XmlName_AutoImport], profile.AutoImport);
				profile.AutoExport = XmlUtil.GetBoolFromNode(profileNode[XmlName_AutoExport], profile.AutoExport);

				profile.LocalUpdate = XmlUtil.GetBoolFromNode(profileNode[XmlName_LocalUpdate], profile.LocalUpdate);
				profile.WebUpdate = XmlUtil.GetBoolFromNode(profileNode[XmlName_WebUpdate], profile.WebUpdate);

				profile.IncludeUnknown = XmlUtil.GetBoolFromNode(profileNode[XmlName_IncludeUnknown], profile.IncludeUnknown);
				profile.BypassIgnoreOnImport = XmlUtil.GetBoolFromNode(profileNode[XmlName_BypassIgnoreOnImport], profile.BypassIgnoreOnImport);

				profile.ExportDiscard = XmlUtil.GetBoolFromNode(profileNode[XmlName_ExportDiscard], profile.ExportDiscard);
				profile.AutoIgnore = XmlUtil.GetBoolFromNode(profileNode[XmlName_AutoIgnore], profile.AutoIgnore);
				profile.OverwriteOnDownload = XmlUtil.GetBoolFromNode(profileNode[XmlName_OverwriteNames], profile.OverwriteOnDownload);

				if (profileVersion < 2)
				{
					if (XmlUtil.TryGetBoolFromNode(profileNode[XmlName_Old_IgnoreExternal], out bool ignoreShortcuts))
					{
						profile.IncludeShortcuts = !ignoreShortcuts;
					}
				}
				else
				{
					profile.IncludeShortcuts = XmlUtil.GetBoolFromNode(profileNode[XmlName_IncludeShortcuts], profile.IncludeShortcuts);
				}

				XmlNode exclusionListNode = profileNode.SelectSingleNode(XmlName_ExclusionList);
				if (exclusionListNode != null)
				{
					XmlNodeList exclusionNodes = exclusionListNode.SelectNodes(XmlName_Exclusion);
					foreach (XmlNode node in exclusionNodes)
					{
						if (XmlUtil.TryGetIntFromNode(node, out int id))
						{
							profile.IgnoreList.Add(id);
						}
					}
				}

				XmlNode gameListNode = profileNode.SelectSingleNode(XmlName_GameList);
				if (gameListNode != null)
				{
					XmlNodeList gameNodes = gameListNode.SelectNodes(XmlName_Game);
					foreach (XmlNode node in gameNodes)
					{
						AddGameFromXmlNode(node, profile, profileVersion);
					}
				}

				XmlNode filterListNode = profileNode.SelectSingleNode(XmlName_FilterList);
				if (filterListNode != null)
				{
					XmlNodeList filterNodes = filterListNode.SelectNodes(XmlName_Filter);
					foreach (XmlNode node in filterNodes)
					{
						AddFilterFromXmlNode(node, profile);
					}
				}

				XmlNode autocatListNode = profileNode.SelectSingleNode(XmlName_AutoCatList);
				if (autocatListNode != null)
				{
					XmlNodeList autoCatNodes = autocatListNode.ChildNodes;
					foreach (XmlNode node in autoCatNodes)
					{
						XmlElement autocatElement = node as XmlElement;
						if (node != null)
						{
							AutoCat autocat = AutoCat.LoadACFromXmlElement(autocatElement);
							if (autocat != null)
							{
								profile.AutoCats.Add(autocat);
							}
						}
					}
				}
				else
				{
					GenerateDefaultAutoCatSet(profile.AutoCats);
				}

				//profile.AutoCats.Sort();
			}

			Logger.Info(GlobalStrings.MainForm_ProfileLoaded);

			return profile;
		}

		// using a list of AutoCat names (strings), return a cloned list of AutoCats replacing the filter with a new one if a new filter is provided.
		// This is used to help process AutoCatGroup.
		public List<AutoCat> CloneAutoCatList(List<string> acList, Filter filter)
		{
			List<AutoCat> newList = new List<AutoCat>();
			foreach (string s in acList)
			{
				// find the AutoCat based on name
				AutoCat ac = GetAutoCat(s);
				if (ac != null)
				{
					// add a cloned copy of the Autocat and replace the filter if one is provided.
					// a cloned copy is used so that the selected property can be assigned without effecting lvAutoCatType on the Main form.
					AutoCat clone = ac.Clone();
					if (filter != null)
					{
						clone.Filter = filter.Name;
					}

					newList.Add(clone);
				}
			}

			return newList;
		}

		public void ExportSteamData()
		{
			GameData.ExportSteamConfig(SteamID64, ExportDiscard, IncludeShortcuts);
		}

		// find and return AutoCat using the name
		public AutoCat GetAutoCat(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}

			foreach (AutoCat ac in AutoCats)
			{
				if (string.Equals(ac.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return ac;
				}
			}

			return null;
		}

		public Image GetAvatar()
		{
			try
			{
				XmlDocument xml = new XmlDocument();
				string profile = string.Format(Constants.UrlSteamProfile, SteamID64);
				xml.Load(profile);

				XmlNodeList xnList = xml.SelectNodes(Constants.XmlNodeAvatar);
				foreach (XmlNode xn in xnList)
				{
					string avatarURL = xn.InnerText;

					return Utility.GetImage(avatarURL, RequestCacheLevel.BypassCache);
				}
			}
			catch
			{
			}

			return null;
		}

		public bool IgnoreGame(int gameId)
		{
			return IgnoreList.Add(gameId);
		}

		public int ImportSteamData()
		{
			return GameData.ImportSteamConfig(SteamID64, IgnoreList, IncludeShortcuts);
		}

		public void Save()
		{
			Save(FilePath);
		}

		public bool Save(string path)
		{
			Logger.Info(GlobalStrings.Profile_SavingProfile, path);
			XmlWriterSettings writeSettings = new XmlWriterSettings
			{
				CloseOutput = true,
				Indent = true
			};

			try
			{
				Utility.BackupFile(path, Settings.Instance.ConfigBackupCount);
			}
			catch (Exception e)
			{
				Logger.Error(GlobalStrings.Log_Profile_ConfigBackupFailed, e.Message);
			}

			XmlWriter writer;
			try
			{
				writer = XmlWriter.Create(path, writeSettings);
			}
			catch (Exception e)
			{
				Logger.Warn(GlobalStrings.Profile_FailedToOpenProfileFile, e.Message);

				throw new ApplicationException(GlobalStrings.Profile_ErrorSavingProfileFile + e.Message, e);
			}

			writer.WriteStartElement(XmlName_Profile);

			writer.WriteAttributeString(XmlName_Version, VERSION.ToString());

			writer.WriteElementString(XmlName_SteamID, SteamID64.ToString());

			writer.WriteElementString(XmlName_AutoUpdate, AutoUpdate.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_AutoImport, AutoImport.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_AutoExport, AutoExport.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_LocalUpdate, LocalUpdate.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_WebUpdate, WebUpdate.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_ExportDiscard, ExportDiscard.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_AutoIgnore, AutoIgnore.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_IncludeUnknown, IncludeUnknown.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_BypassIgnoreOnImport, BypassIgnoreOnImport.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_OverwriteNames, OverwriteOnDownload.ToString().ToLowerInvariant());
			writer.WriteElementString(XmlName_IncludeShortcuts, IncludeShortcuts.ToString().ToLowerInvariant());

			writer.WriteStartElement(XmlName_GameList);

			foreach (GameInfo g in GameData.Games.Values)
			{
				if (IncludeShortcuts || (g.Id > 0))
				{
					// Don't save shortcuts if we aren't including them
					writer.WriteStartElement(XmlName_Game);

					writer.WriteElementString(XmlName_Game_Id, g.Id.ToString());
					writer.WriteElementString(XmlName_Game_Source, g.Source.ToString());

					if (g.Name != null)
					{
						writer.WriteElementString(XmlName_Game_Name, g.Name);
					}

					writer.WriteElementString(XmlName_Game_Hidden, g.Hidden.ToString().ToLowerInvariant());

					if (g.LastPlayed != 0)
					{
						writer.WriteElementString(XmlName_Game_LastPlayed, g.LastPlayed.ToString());
					}

					if (!g.Executable.Contains("steam://"))
					{
						writer.WriteElementString(XmlName_Game_Executable, g.Executable);
					}

					writer.WriteStartElement(XmlName_Game_CategoryList);
					foreach (Category c in g.Categories)
					{
						string catName = c.Name;
						if (c.Name == GameList.FAVORITE_NEW_CONFIG_VALUE)
						{
							catName = GameList.FAVORITE_CONFIG_VALUE;
						}

						writer.WriteElementString(XmlName_Game_Category, catName);
					}

					writer.WriteEndElement(); // categories

					writer.WriteEndElement(); // game
				}
			}

			writer.WriteEndElement(); // games

			writer.WriteStartElement(XmlName_FilterList);

			foreach (Filter f in GameData.Filters)
			{
				f.WriteToXml(writer);
			}

			writer.WriteEndElement(); //game filters

			writer.WriteStartElement(XmlName_AutoCatList);

			foreach (AutoCat autocat in AutoCats)
			{
				autocat.WriteToXml(writer);
			}

			writer.WriteEndElement(); //autocats

			writer.WriteStartElement(XmlName_ExclusionList);

			foreach (int i in IgnoreList)
			{
				writer.WriteElementString(XmlName_Exclusion, i.ToString());
			}

			writer.WriteEndElement(); // exclusions

			writer.WriteEndElement(); // profile

			writer.Close();
			FilePath = path;
			Logger.Info(GlobalStrings.Profile_ProfileSaveComplete);

			return true;
		}

		#endregion

		#region Methods

		private static void AddFilterFromXmlNode(XmlNode node, Profile profile)
		{
			if (!XmlUtil.TryGetStringFromNode(node[XmlName_FilterName], out string name))
			{
				return;
			}

			Filter f = profile.GameData.AddFilter(name);
			if (XmlUtil.TryGetIntFromNode(node[XmlName_FilterGame], out int game))
			{
				f.Game = game;
			}

			if (XmlUtil.TryGetIntFromNode(node[XmlName_FilterHidden], out int hidden))
			{
				f.Hidden = hidden;
			}

			if (XmlUtil.TryGetIntFromNode(node[XmlName_FilterSoftware], out int software))
			{
				f.Software = software;
			}

			if (XmlUtil.TryGetIntFromNode(node[XmlName_FilterUncategorized], out int uncategorized))
			{
				f.Uncategorized = uncategorized;
			}

			if (XmlUtil.TryGetIntFromNode(node[XmlName_FilterVR], out int vr))
			{
				f.VR = vr;
			}

			XmlNodeList filterNodes = node.SelectNodes(XmlName_FilterAllow);
			if (filterNodes != null)
			{
				foreach (XmlNode fNode in filterNodes)
				{
					if (XmlUtil.TryGetStringFromNode(fNode, out string catName))
					{
						f.Allow.Add(profile.GameData.GetCategory(catName));
					}
				}
			}

			filterNodes = node.SelectNodes(XmlName_FilterRequire);
			if (filterNodes != null)
			{
				foreach (XmlNode fNode in filterNodes)
				{
					if (XmlUtil.TryGetStringFromNode(fNode, out string catName))
					{
						f.Require.Add(profile.GameData.GetCategory(catName));
					}
				}
			}

			filterNodes = node.SelectNodes(XmlName_FilterExclude);
			if (filterNodes != null)
			{
				foreach (XmlNode fNode in filterNodes)
				{
					if (XmlUtil.TryGetStringFromNode(fNode, out string catName))
					{
						f.Exclude.Add(profile.GameData.GetCategory(catName));
					}
				}
			}
		}

		private static void AddGameFromXmlNode(XmlNode node, Profile profile, int profileVersion)
		{
			if (XmlUtil.TryGetIntFromNode(node[XmlName_Game_Id], out int id))
			{
				GameListingSource source = XmlUtil.GetEnumFromNode(node[XmlName_Game_Source], GameListingSource.Unknown);

				if ((source < GameListingSource.Manual) && profile.IgnoreList.Contains(id))
				{
					return;
				}

				string name = XmlUtil.GetStringFromNode(node[XmlName_Game_Name], null);
				GameInfo game = new GameInfo(id, name, profile.GameData)
				{
					Source = source
				};

				profile.GameData.Games.Add(id, game);

				game.Hidden = XmlUtil.GetBoolFromNode(node[XmlName_Game_Hidden], false);
				game.Executable = XmlUtil.GetStringFromNode(node[XmlName_Game_Executable], null);
				game.LastPlayed = XmlUtil.GetIntFromNode(node[XmlName_Game_LastPlayed], 0);

				if (profileVersion < 1)
				{
					if (XmlUtil.TryGetStringFromNode(node[XmlName_Game_Category], out string catName))
					{
						game.AddCategory(profile.GameData.GetCategory(catName));
					}

					if (node.SelectSingleNode(XmlName_Old_Game_Favorite) != null)
					{
						game.SetFavorite(true);
					}
				}
				else
				{
					XmlNode catListNode = node.SelectSingleNode(XmlName_Game_CategoryList);
					if (catListNode != null)
					{
						XmlNodeList catNodes = catListNode.SelectNodes(XmlName_Game_Category);
						foreach (XmlNode cNode in catNodes)
						{
							if (XmlUtil.TryGetStringFromNode(cNode, out string cat))
							{
								game.AddCategory(profile.GameData.GetCategory(cat));
							}
						}
					}
				}
			}
		}

		#endregion
	}
}
