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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Depressurizer.Core;
using Depressurizer.Core.Helpers;

namespace Depressurizer
{
	public partial class DlgProfile : Form
	{
		#region Fields

		public Profile Profile;

		private readonly bool editMode;

		private int currentThreadCount;

		private ThreadLocker currentThreadLock = new ThreadLocker();

		#endregion

		#region Constructors and Destructors

		public DlgProfile()
		{
			InitializeComponent();
		}

		public DlgProfile(Profile profile) : this()
		{
			Profile = profile;
			editMode = true;
		}

		#endregion

		#region Delegates

		private delegate void SimpleDelegate();

		private delegate void UpdateDelegate(int i, string s);

		#endregion

		#region Public Properties

		public bool DownloadNow => chkActUpdate.Checked;

		public bool ImportNow => chkActImport.Checked;

		public bool SetStartup => chkSetStartup.Checked;

		#endregion

		#region Properties

		private static Logger Logger => Logger.Instance;

		#endregion

		#region Public Methods and Operators

		public string GetDisplayName(long accountId)
		{
			try
			{
				XmlDocument doc = new XmlDocument();
				HttpWebRequest req = (HttpWebRequest) WebRequest.Create(string.Format("http://www.steamcommunity.com/profiles/{0}?xml=true", accountId));
				using (WebResponse resp = req.GetResponse())
				{
					doc.Load(resp.GetResponseStream());
				}

				XmlNode nameNode = doc.SelectSingleNode("profile/steamID");
				if (nameNode != null)
				{
					return nameNode.InnerText;
				}
			}
			catch (Exception e)
			{
				Logger.Warn(GlobalStrings.DlgProfile_ExceptionRaisedWhenTryingScrapeProfileName, accountId);
				Logger.Warn(e.Message);
			}

			return null;
		}

		#endregion

		#region Methods

		private bool Apply()
		{
			if (radSelUserByURL.Checked)
			{
				CDlgGetSteamID dlg = new CDlgGetSteamID(txtUserUrl.Text);
				dlg.ShowDialog();

				if (dlg.DialogResult == DialogResult.Cancel)
				{
					return false;
				}

				if ((dlg.Success == false) || (dlg.SteamID == 0))
				{
					MessageBox.Show(this, GlobalStrings.DlgProfile_CouldNotFindSteamProfile, GlobalStrings.DBEditDlg_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

					return false;
				}

				txtUserID.Text = dlg.SteamID.ToString();
			}

			if (editMode)
			{
				if (ValidateEntries())
				{
					SaveModifiables(Profile);

					return true;
				}

				return false;
			}

			return CreateProfile();
		}

		private void cmdBrowse_Click(object sender, EventArgs e)
		{
			SaveFileDialog dlg = new SaveFileDialog();

			try
			{
				FileInfo f = new FileInfo(txtFilePath.Text);
				dlg.InitialDirectory = f.DirectoryName;
				dlg.FileName = f.Name;
			}
			catch (ArgumentException)
			{
			}

			dlg.DefaultExt = "profile";
			dlg.AddExtension = true;
			dlg.Filter = GlobalStrings.DlgProfile_Filter;
			DialogResult res = dlg.ShowDialog();
			if (res == DialogResult.OK)
			{
				txtFilePath.Text = dlg.FileName;
			}
		}

		private void cmdCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}

		private void cmdIgnore_Click(object sender, EventArgs e)
		{
			if (int.TryParse(txtIgnore.Text, out int id))
			{
				lstIgnored.Items.Add(id.ToString());
				txtIgnore.ResetText();
				lstIgnored.Sort();
			}
			else
			{
				MessageBox.Show(GlobalStrings.DlgGameDBEntry_IDMustBeInteger, GlobalStrings.Gen_Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private void cmdOk_Click(object sender, EventArgs e)
		{
			if (Apply())
			{
				DialogResult = DialogResult.OK;
				Close();
			}
		}

		private void cmdUnignore_Click(object sender, EventArgs e)
		{
			while (lstIgnored.SelectedIndices.Count > 0)
			{
				lstIgnored.Items.RemoveAt(lstIgnored.SelectedIndices[0]);
			}
		}

		private void cmdUserUpdate_Click(object sender, EventArgs e)
		{
			StartThreadedNameUpdate();
		}

		private void cmdUserUpdateCancel_Click(object sender, EventArgs e)
		{
			if (currentThreadCount > 0)
			{
				lock (currentThreadLock)
				{
					currentThreadLock.Aborted = true;
				}

				SetUpdateInterfaceStopping();
			}
		}

		private bool CreateProfile()
		{
			if (!ValidateEntries())
			{
				return false;
			}

			FileInfo file;
			try
			{
				file = new FileInfo(txtFilePath.Text);
			}
			catch
			{
				MessageBox.Show(GlobalStrings.DlgProfile_YouMustEnterValidProfilePath, GlobalStrings.DBEditDlg_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return false;
			}

			if (!file.Directory.Exists)
			{
				try
				{
					file.Directory.Create();
				}
				catch
				{
					MessageBox.Show(GlobalStrings.DlgProfile_FailedToCreateParentDirectory, GlobalStrings.DBEditDlg_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

					return false;
				}
			}

			Profile profile = new Profile();

			SaveModifiables(profile);
			Profile.GenerateDefaultAutoCatSet(profile.AutoCats);

			try
			{
				profile.Save(file.FullName);
			}
			catch (ApplicationException e)
			{
				MessageBox.Show(e.Message, GlobalStrings.DBEditDlg_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return false;
			}

			Profile = profile;

			return true;
		}

		/// <summary>
		///     Gets a list of located account ids. Uses settings for the steam path.
		/// </summary>
		/// <returns>An array of located IDs</returns>
		private string[] GetSteamIds()
		{
			try
			{
				DirectoryInfo dir = new DirectoryInfo(Settings.Instance.SteamPath + "\\userdata");
				if (dir.Exists)
				{
					DirectoryInfo[] userDirs = dir.GetDirectories();
					string[] result = new string[userDirs.Length];
					for (int i = 0; i < userDirs.Length; i++)
					{
						result[i] = userDirs[i].Name;
					}

					return result;
				}

				return new string[0];
			}
			catch
			{
				return new string[0];
			}
		}

		private void InitializeEditMode()
		{
			txtFilePath.Text = Profile.FilePath;
			grpProfInfo.Enabled = false;

			chkActUpdate.Checked = false;
			chkActImport.Checked = false;
			chkSetStartup.Checked = false;

			chkAutoUpdate.Checked = Profile.AutoUpdate;
			chkAutoImport.Checked = Profile.AutoImport;
			chkLocalUpdate.Checked = Profile.LocalUpdate;
			chkWebUpdate.Checked = Profile.WebUpdate;
			chkExportDiscard.Checked = Profile.ExportDiscard;
			chkIncludeShortcuts.Checked = Profile.IncludeShortcuts;
			chkOverwriteNames.Checked = Profile.OverwriteOnDownload;

			Text = GlobalStrings.DlgProfile_EditProfile;

			chkAutoIgnore.Checked = Profile.AutoIgnore;
			chkIncludeUnknown.Checked = Profile.IncludeUnknown;
			chkBypassIgnoreOnImport.Checked = Profile.BypassIgnoreOnImport;

			foreach (int i in Profile.IgnoreList)
			{
				lstIgnored.Items.Add(i.ToString());
			}

			lstIgnored.Sort();

			bool found = SelectUserInList(Profile.SteamID64);
			if (found)
			{
				radSelUserFromList.Checked = true;
			}
			else
			{
				radSelUserByID.Checked = true;
				txtUserID.Text = Profile.SteamID64.ToString();
			}
		}

		/// <summary>
		///     Populates the combo box with all located account IDs
		/// </summary>
		private void LoadShortIds()
		{
			lstUsers.BeginUpdate();

			lstUsers.Items.Clear();

			string[] ids = GetSteamIds();

			foreach (string id in ids)
			{
				lstUsers.Items.Add(new UserRecord(id));
			}

			lstUsers.EndUpdate();
		}

		private void lstUsers_SelectedIndexChanged(object sender, EventArgs e)
		{
			UserRecord u = lstUsers.SelectedItem as UserRecord;
			if (u != null)
			{
				txtUserID.Text = Profile.DirNametoID64(u.DirName).ToString();
			}
		}

		private void NameUpdateThread(object d)
		{
			UpdateData data = (UpdateData) d;
			bool abort = false;
			do
			{
				UpdateJob job = null;
				lock (data.jobs)
				{
					if (data.jobs.Count > 0)
					{
						job = data.jobs.Dequeue();
					}
					else
					{
						abort = true;
					}
				}

				if (job != null)
				{
					string name = GetDisplayName(Profile.DirNametoID64(job.dir));

					lock (data.tLock)
					{
						if (data.tLock.Aborted)
						{
							abort = true;
						}
						else
						{
							UpdateDisplayNameInList(job.index, name);
						}
					}
				}
			} while (!abort);

			OnNameUpdateThreadTerminate();
		}

		private void OnNameUpdateThreadTerminate()
		{
			if (InvokeRequired)
			{
				Invoke(new SimpleDelegate(OnNameUpdateThreadTerminate));
			}
			else
			{
				currentThreadCount--;
				if (currentThreadCount == 0)
				{
					SetUpdateInterfaceNormal();
				}
			}
		}

		private void ProfileDlg_FormClosing(object sender, FormClosingEventArgs e)
		{
			lock (currentThreadLock)
			{
				currentThreadLock.Aborted = true;
			}
		}

		private void ProfileDlg_Load(object sender, EventArgs e)
		{
			ttHelp.Ext_SetToolTip(lblHelp_ExportDiscard, GlobalStrings.DlgProfile_Help_ExportDiscard);
			ttHelp.Ext_SetToolTip(lblHelp_LocalUpdate, GlobalStrings.DlgProfile_Help_LocalUpdate);
			ttHelp.Ext_SetToolTip(lblHelp_WebUpdate, GlobalStrings.DlgProfile_Help_WebUpdate);
			ttHelp.Ext_SetToolTip(lblHelp_IncludeUnknown, GlobalStrings.DlgProfile_Help_IncludeUnknown);
			ttHelp.Ext_SetToolTip(lblHelp_BypassIgnoreOnImport, GlobalStrings.DlgProfile_Help_BypassIgnoreOnImport);

			LoadShortIds();
			if (editMode)
			{
				InitializeEditMode();
			}
			else
			{
				txtFilePath.Text = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Depressurizer\Default.profile";

				if (lstUsers.Items.Count == 0)
				{
					MessageBox.Show(GlobalStrings.DlgProfile_NoAccountConfiguration, GlobalStrings.Gen_Warning, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					radSelUserByURL.Checked = true;
				}
				else
				{
					radSelUserFromList.Checked = true;
				}

				StartThreadedNameUpdate();
			}

			lstIgnored.ListViewItemSorter = new IgnoreListViewItemComparer();
		}

		private void radSelUser_CheckedChanged(object sender, EventArgs e)
		{
			lstUsers.Enabled = radSelUserFromList.Checked;
			lstUsers.SelectedItem = null;
			txtUserID.Enabled = radSelUserByID.Checked;
			txtUserUrl.Enabled = radSelUserByURL.Checked;

			if (radSelUserFromList.Checked)
			{
				SelectUserInList(txtUserID.Text);
			}
		}

		private void SaveModifiables(Profile p)
		{
			p.SteamID64 = long.Parse(txtUserID.Text);

			p.AutoUpdate = chkAutoUpdate.Checked;
			p.AutoImport = chkAutoImport.Checked;
			p.LocalUpdate = chkLocalUpdate.Checked;
			p.WebUpdate = chkWebUpdate.Checked;
			p.ExportDiscard = chkExportDiscard.Checked;
			p.IncludeShortcuts = chkIncludeShortcuts.Checked;
			p.OverwriteOnDownload = chkOverwriteNames.Checked;

			p.AutoIgnore = chkAutoIgnore.Checked;
			p.IncludeUnknown = chkIncludeUnknown.Checked;
			p.BypassIgnoreOnImport = chkBypassIgnoreOnImport.Checked;

			SortedSet<int> ignoreSet = new SortedSet<int>();
			foreach (ListViewItem item in lstIgnored.Items)
			{
				if (int.TryParse(item.Text, out int id))
				{
					ignoreSet.Add(id);
				}
			}

			p.IgnoreList = ignoreSet;
		}

		private bool SelectUserInList(long accountId)
		{
			string profDirName = Profile.ID64toDirName(accountId);

			for (int i = 0; i < lstUsers.Items.Count; i++)
			{
				UserRecord r = lstUsers.Items[i] as UserRecord;
				if ((r != null) && (r.DirName == profDirName))
				{
					lstUsers.SelectedIndex = i;

					return true;
				}
			}

			return false;
		}

		private bool SelectUserInList(string accountId)
		{
			if (long.TryParse(accountId, out long val))
			{
				return SelectUserInList(val);
			}

			return false;
		}

		private void SetUpdateInterfaceNormal()
		{
			cmdUserUpdate.Enabled = true;
			cmdUserUpdateCancel.Enabled = false;
			lblUserStatus.Text = GlobalStrings.DlgProfile_ClickUpdateToDisplayNames;
		}

		private void SetUpdateInterfaceRunning()
		{
			cmdUserUpdate.Enabled = false;
			cmdUserUpdateCancel.Enabled = true;
			lblUserStatus.Text = GlobalStrings.DlgProfile_UpdatingNames;
		}

		private void SetUpdateInterfaceStopping()
		{
			cmdUserUpdate.Enabled = false;
			cmdUserUpdateCancel.Enabled = false;
			lblUserStatus.Text = GlobalStrings.DlgProfile_Cancelling;
		}

		private void StartThreadedNameUpdate()
		{
			if (currentThreadCount > 0)
			{
				return;
			}

			int maxThreads = 1;

			Queue<UpdateJob> q = new Queue<UpdateJob>();
			for (int i = 0; i < lstUsers.Items.Count; i++)
			{
				UserRecord r = lstUsers.Items[i] as UserRecord;
				if (r != null)
				{
					q.Enqueue(new UpdateJob(i, r.DirName));
				}
			}

			int threads = maxThreads > q.Count ? maxThreads : q.Count;

			if (threads > 0)
			{
				currentThreadLock = new ThreadLocker();
				SetUpdateInterfaceRunning();
				for (int i = 0; i < threads; i++)
				{
					Thread t = new Thread(NameUpdateThread);
					currentThreadCount++;
					t.Start(new UpdateData(q, currentThreadLock));
				}
			}
		}

		private void txtUserID_TextChanged(object sender, EventArgs e)
		{
			//    if( !skipUserClear ) {
			//        lstUsers.ClearSelected();
			//    }
		}

		private void UpdateDisplayNameInList(int index, string name)
		{
			if (InvokeRequired)
			{
				Invoke(new UpdateDelegate(UpdateDisplayNameInList), index, name);
			}
			else
			{
				UserRecord u = lstUsers.Items[index] as UserRecord;
				if (u != null)
				{
					bool selected = lstUsers.SelectedIndex == index;
					if (name == null)
					{
						name = "?";
					}

					u.DisplayName = name;

					lstUsers.Items.RemoveAt(index);
					lstUsers.Items.Insert(index, u);
					lstUsers.SelectedIndex = selected ? index : 0;
				}
			}
		}

		private bool ValidateEntries()
		{
			if (!long.TryParse(txtUserID.Text, out long id))
			{
				MessageBox.Show(GlobalStrings.DlgProfile_AccountIDMustBeNumber, GlobalStrings.DBEditDlg_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

				return false;
			}

			return true;
		}

		#endregion

		public class ThreadLocker
		{
			#region Public Properties

			public bool Aborted { get; set; }

			#endregion
		}

		public class UpdateData
		{
			#region Fields

			public Queue<UpdateJob> jobs;

			public ThreadLocker tLock;

			#endregion

			#region Constructors and Destructors

			public UpdateData(Queue<UpdateJob> q, ThreadLocker l)
			{
				jobs = q;
				tLock = l;
			}

			#endregion
		}

		public class UpdateJob
		{
			#region Fields

			public string dir;

			public int index;

			#endregion

			#region Constructors and Destructors

			public UpdateJob(int i, string d)
			{
				index = i;
				dir = d;
			}

			#endregion
		}

		public class UserRecord
		{
			#region Fields

			public string DirName;

			public string DisplayName;

			#endregion

			#region Constructors and Destructors

			public UserRecord(string dir)
			{
				DirName = dir;
			}

			#endregion

			#region Public Methods and Operators

			public override string ToString()
			{
				if (DisplayName == null)
				{
					return DirName;
				}

				return string.Format("{0} - {1}", DirName, DisplayName);
			}

			#endregion
		}

		/*
	    private void NonthreadedNameUpdate() {
	        for( int i = 0; i < lstUsers.Items.Count; i++ ) {
	            UserRecord u = lstUsers.Items[i] as UserRecord;
	            if( u != null ) {
	                string name = GetDisplayName( Profile.DirNametoID64( u.DirName ) );
	                if( name == null ) {
	                    u.DisplayName = "?";
	                } else {
	                    u.DisplayName = name;
	                }
	            }
	            lstUsers.Items.RemoveAt( i );
	            lstUsers.Items.Insert( i, u );
	        }
	    }
	    */
	}

	internal class IgnoreListViewItemComparer : IComparer
	{
		#region Public Methods and Operators

		public int Compare(object x, object y)
		{
			if (int.TryParse(((ListViewItem) x).Text, out int a) && int.TryParse(((ListViewItem) y).Text, out int b))
			{
				return a - b;
			}

			return string.Compare(((ListViewItem) x).Text, ((ListViewItem) y).Text);
		}

		#endregion
	}
}
