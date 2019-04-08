using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using NAudio.SoundFont;

namespace Commons.Music.SoundFontPlayer
{
	public class Model
	{
		const string config_filename = "soundfont-player.cfg";

		public void Initialize ()
		{
			LoadUserSettings ();
		}

		public UserSettings UserSettings { get; private set; } = new UserSettings ();

		void LoadUserSettings ()
		{
			using (var ds = IsolatedStorageFile.GetUserStoreForAssembly ()) {
				if (!ds.FileExists (config_filename))
					return;

				using (var stream = ds.OpenFile (config_filename, FileMode.Open)) {
					UserSettings =
						(UserSettings) new DataContractJsonSerializer (typeof (UserSettings))
							.ReadObject (stream);
				}
			}
			OnSoundFontPathsUpdated ();
		}

		void SaveUserSettings ()
		{
			using (var ds = IsolatedStorageFile.GetUserStoreForAssembly ())
			using (var stream = ds.OpenFile (config_filename, FileMode.Create))
				new DataContractJsonSerializer (typeof (UserSettings)).WriteObject (stream,
					UserSettings);
		}
		
		// data store manipulation

		public class SoundFontEntity
		{
			public string FullPath { get; set; }
			
			public bool IsInvalid { get; set; }

			public Lazy<SoundFont> Entity => new Lazy<SoundFont> (() => {

				try {
					return new NAudio.SoundFont.SoundFont (FullPath);
				}
				catch (Exception ex) {
					IsInvalid = true;
					return null;
				}
			});
		}

		public IList<SoundFontEntity> LoadedSoundFonts { get; private set; } = new List<SoundFontEntity> ();

		public void UpdateSoundFontList (string [] soundFontFiles)
		{
			LoadedSoundFonts = soundFontFiles.Select (s => new SoundFontEntity {FullPath = s}).ToList ();
			if (SoundFontListUpdated != null)
				SoundFontListUpdated ();
		}

		public event Action SoundFontListUpdated;

		public void UpdateDirectoryList (string [] directories)
		{
			UserSettings.SoundFontPaths = new List<string> ();
			foreach (var dir in directories)
				UserSettings.SoundFontPaths.Add (dir);
			SaveUserSettings ();
			OnSoundFontPathsUpdated ();
		}

		void OnSoundFontPathsUpdated ()
		{
			if (SoundFontPathsUpdated != null)
				SoundFontPathsUpdated ();
			
			UpdateSoundFontList (UserSettings.SoundFontPaths.SelectMany (d =>
				Directory.GetFiles (d, "*.sf2").Concat (Directory.GetFiles (d, "*.sf3"))).ToArray ());
		}

		public event Action SoundFontPathsUpdated;
	}
}