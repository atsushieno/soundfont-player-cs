using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Commons.Music.Midi;
using NFluidsynth.MidiManager;
using NAudio.SoundFont;

namespace Commons.Music.SoundFontPlayer
{
	public class Model
	{
		const string config_filename = "soundfont-player.cfg";

		public async Task Initialize ()
		{
			await LoadUserSettings ();
		}

		public void Terminate ()
		{
			if (output != null)
				output.CloseAsync ();
		}

		public UserSettings UserSettings { get; private set; } = new UserSettings ();

		async Task LoadUserSettings ()
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
			await OnSoundFontPathsUpdated ();
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

			public SoundFont Entity { get; set; }

			public async Task Load ()
			{
				try {
					Entity = new SoundFont (FullPath);
				} catch (Exception ex) {
					IsInvalid = true;
				}
			}
		}

		public IList<SoundFontEntity> LoadedSoundFonts { get; private set; } = new List<SoundFontEntity> ();

		public void UpdateSoundFontList (string [] soundFontFiles)
		{
			LoadedSoundFonts = soundFontFiles.Select (s => new SoundFontEntity {FullPath = s}).ToList ();
			if (SoundFontListUpdated != null)
				SoundFontListUpdated ();
		}

		public event Action SoundFontListUpdated;
		bool skip_loading;

		public void UpdateDirectoryList (string [] directories)
		{
			skip_loading = true;
			try {
				UserSettings.SoundFontPaths = new List<string> ();
				foreach (var dir in directories)
					UserSettings.SoundFontPaths.Add (dir);
				SaveUserSettings ();
				OnSoundFontPathsUpdated ();
			}
			finally {
				skip_loading = false;
			}
		}

		async Task OnSoundFontPathsUpdated ()
		{
			if (SoundFontPathsUpdated != null)
				SoundFontPathsUpdated ();
			
			UpdateSoundFontList (UserSettings.SoundFontPaths.SelectMany (d =>
				Directory.GetFiles (d, "*.sf2").Concat (Directory.GetFiles (d, "*.sf3"))).ToArray ());
		}

		public event Action SoundFontPathsUpdated;

		public class SoundFontSelection
		{
			public string File { get; set; }
			public int Bank { get; set; }
			public int Patch { get; set; }
		}

		public SoundFontSelection SelectedSoundItem { get; set; }

		FluidsynthMidiAccess access;
		IMidiOutput output;

		public void UpdateSelectedInstrument (SoundFontSelection item)
		{
			if (skip_loading)
				return;

			var prevItem = SelectedSoundItem;
			SelectedSoundItem = item;
			
			if (output != null && item.File != prevItem.File) {
				output.CloseAsync ();
				keyon_flags = new bool [128];
			}

			if (prevItem?.File != item.File) {
				access = new FluidsynthMidiAccess ();
				if (File.Exists ("/dev/snd/seq")) // ALSA
					access.ConfigureSettings = s =>
						s [NFluidsynth.ConfigurationKeys.AudioDriver].StringValue = "alsa";
				access.SoundFonts.Add (item.File);
				output = access.OpenOutputAsync (access.Outputs.First ().Id).Result;
			}

			int ch = item.Bank >= 128 ? 9 : 0;

			/*
			output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.BankSelect, (byte) (item.Bank / 0x80)}, 0, 3, 0);
			output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.BankSelectLsb, (byte) (item.Bank % 0x80)}, 0, 3, 0);
			output.Send (new byte[] {(byte) (MidiEvent.Program + ch), (byte) (item.Instrument % 128)}, 0, 2, 0);
			output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.Volume, 120}, 0, 3, 0);
			*/
			output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.BankSelect, (byte) (item.Bank / 0x80)}, 0, 3, 0);
			output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.BankSelectLsb, (byte) (item.Bank % 0x80)}, 0, 3, 0);
			output.Send (new byte[] {(byte) (MidiEvent.Program + ch), (byte) (item.Patch % 128)}, 0, 2, 0);
			output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.Volume, 127}, 0, 3, 0);
			output.Send (new byte[] {(byte) (MidiEvent.CC + ch), MidiCC.Expression, 127}, 0, 3, 0);
		}

		bool [] keyon_flags = new bool[128];

		public void PlayNote (int key, int lengthInMilliseconds)
		{
			if (output == null)
				return;

			int ch = SelectedSoundItem.Bank >= 128 ? 9 : 0;
			
			if (keyon_flags [key])
				output.Send (new byte[]{(byte) (MidiEvent.NoteOn + ch), (byte) key, 0}, 0, 3, 0);
			output.Send (new byte[]{(byte) (MidiEvent.NoteOn + ch), (byte) key, 120}, 0, 3, 0);
			keyon_flags [key] = true;
			if (lengthInMilliseconds > 0)
				Task.Run (() => {
					Thread.Sleep (lengthInMilliseconds);
					if (keyon_flags [key]) // might be already turned off
						output.Send (new byte[]{(byte)(MidiEvent.NoteOn + ch), (byte) key, 0}, 0, 3, 0);
					keyon_flags [key] = false;
				});
		}
	}
}
