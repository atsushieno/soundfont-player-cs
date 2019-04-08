using System;
using Xwt;

namespace Commons.Music.SoundFontPlayer
{
	internal class Program
	{
		[STAThread]
		public static void Main (string [] args)
		{
			Application.Initialize ();
			new MainWindow ().Show ();
			Application.Run ();
		}
	}
}