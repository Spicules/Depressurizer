﻿#region License

//     This file (Program.cs) is part of Depressurizer.
//     Copyright (C) 2011  Steve Labbe
//     Copyright (C) 2018  Martijn Vegter
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Windows.Forms;
using Rallion;

namespace Depressurizer
{
	internal static class Program
	{
		public static AppLogger Logger;
		public static GameDB GameDB;

		/// <summary>
		///     The main entry point for the application.
		/// </summary>
		[STAThread]
		private static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.ApplicationExit += OnApplicationExit;

			FatalError.InitializeHandler();

			Logger = new AppLogger
			{
				Level = LoggerLevel.None,
				DateFormat = "HH:mm:ss'.'ffffff",
				MaxFileSize = 2000000,
				MaxBackup = 1,
				FileNameTemplate = "Depressurizer.log"
			};

			Settings.Instance.Load();

			Logger.Write(LoggerLevel.Info, GlobalStrings.Program_ProgramInitialized, Logger.Level);
			Logger.Write(LoggerLevel.Info, "Automatic mode not set, loading main form.");

			Application.Run(new FormMain());
		}

		private static void OnApplicationExit(object sender, EventArgs e)
		{
			Settings.Instance.Save();

			Logger.Write(LoggerLevel.Info, GlobalStrings.Program_ProgramClosing);
			Logger.EndSession();
		}
	}
}