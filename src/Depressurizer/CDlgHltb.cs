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

using Depressurizer.Core;
using Rallion;

namespace Depressurizer
{
	internal class HltbPrcDlg : CancelableDlg
	{
		#region Constructors and Destructors

		public HltbPrcDlg() : base(GlobalStrings.CDlgHltb_Title, false)
		{
			SetText(GlobalStrings.CDlgHltb_UpdateHltb);
			Updated = 0;
		}

		#endregion

		#region Public Properties

		public int Updated { get; private set; }

		#endregion

		#region Properties

		private static Database Database => Database.Instance;

		#endregion

		#region Methods

		protected override void Finish()
		{
			if (!Canceled && (Error == null))
			{
				OnJobCompletion();
			}
		}

		protected override void RunProcess()
		{
			Updated = Database.UpdateFromHltb(Settings.Instance.IncludeImputedTimes);
			OnThreadCompletion();
		}

		#endregion
	}
}
