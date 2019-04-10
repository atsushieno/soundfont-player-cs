using System;
using System.Linq;
using System.Threading.Tasks;
using NAudio.SoundFont;
using Xwt;
using Xwt.WPFBackend;

namespace Commons.Music.SoundFontPlayer
{
	public class MainWindow : Window
	{
		public MainWindow ()
		{
			Title = "NFluidsynth SoundFont Player";
			Width = 900;
			Height = 700;
			this.Closed += (o, e) => {
				model.Terminate ();
				Application.Exit ();
			};

			model = new Model ();
			
			Application.InvokeAsync (() => {
				SetupWindowContent ();
				Application.InvokeAsync (() => {
					model.Initialize ().Wait ();
				});
			});
		}

		Model model;

		void SetupWindowContent ()
		{
			var top = new VBox ();
			
			top.PackStart (CreateSoundFontFoldersPanel ());

			var sub = new HBox () { ExpandVertical = true, ExpandHorizontal = true};
			top.PackStart (sub, true);

			sub.PackStart (CreateSoundFontListPanel (), true);

			var panel = new ScrollView () { ExpandVertical = true, ExpandHorizontal = true };
			var keyRows = new VBox () { ExpandVertical = true, ExpandHorizontal = true };
			panel.Content = keyRows;
			sub.PackStart (panel, true);
			
			string [] noteNames = {"c", "c+", "d", "d+", "e", "f", "f+", "g", "g+", "a", "a+", "b"};
			int nRows = 13;
			int nCols = 10;
			for (int r = 0; r < nRows; r++) {
				var keys = new HBox ();
				for (int i = 0; i < nCols; i++) {
					int key = r * nCols + i;
					if (key >= 128)
						break;
					var b = new Button ($"{key / 12}{noteNames [key % 12]}") {Tag = key, WidthRequest = 36};
					b.Clicked += (o, e) => PlayNote ((int) b.Tag);
					keys.PackStart (b);
				}

				keyRows.PackStart (keys);
			}

			Content = top;
		}

		void PlayNote (int key)
		{
			model.PlayNote (key, 2000);
		}

		Action update_soundfonts;

		Widget CreateSoundFontListPanel ()
		{
			var sfList =new TreeView () { UseAlternatingRowColors = true, AnimationsEnabled = true };
			var nameCol = new DataField<string> ();
			var bankCol = new DataField<int> ();
			var fileCol = new DataField<string> ();
			var presetCol = new DataField<string> ();
			var namePresetCol = new DataField<string> ();
			var patchCol = new DataField<int> ();
			var store = new TreeStore (fileCol, nameCol, bankCol, patchCol, presetCol, namePresetCol);
			sfList.DataSource = store;
			//sfList.Columns.Add ("File", fileCol);
			//sfList.Columns.Add ("Name", nameCol);
			sfList.Columns.Add ("Bank", bankCol);
			sfList.Columns.Add ("Patch", patchCol);
			//sfList.Columns.Add ("Preset", presetCol);
			sfList.Columns.Add ("Name/Preset", namePresetCol);
			
			update_soundfonts = () => {
				store.Clear ();
				foreach (var sf in model.LoadedSoundFonts)
					sf.Load ();
				foreach (var sf in model.LoadedSoundFonts.Where (_ => _.Entity != null)) {
					foreach (var preset in sf.Entity.Presets.OrderBy (p => p.Bank).ThenBy (p => p.PatchNumber)) {
						var node = store.AddNode ();
						node.SetValues (
							fileCol, sf.FullPath, 
							nameCol, sf.Entity.FileInfo.BankName,
							bankCol, preset.Bank,
							patchCol, preset.PatchNumber,
							presetCol, preset.Name,
							namePresetCol, sf.Entity.FileInfo.BankName + " / " + preset.Name);
					}
				}
			};
			model.SoundFontListUpdated += update_soundfonts;

			sfList.SelectionChanged += (o, e) => {
				var iter = store.GetNavigatorAt (sfList.SelectedRow);
				model.UpdateSelectedInstrument (new Model.SoundFontSelection {
					File = iter.GetValue (fileCol),
					Patch = iter.GetValue (patchCol),
					Bank = iter.GetValue (bankCol),
				});
			};
			
			return sfList;
		}
		
		Widget CreateSoundFontFoldersPanel ()
		{
			var top = new HBox { Name = "SoundFontFoldersPanel"};
			
			var dirlist = new ListView { SelectionMode = SelectionMode.Multiple };
			var df = new DataField<string> ();
			var liststore = new ListStore (df);
			dirlist.DataSource = liststore;
			dirlist.Columns.Add ("Directory", df);
			Action updateSoundFontPaths = () => {
				liststore.Clear ();
				for (int i = 0; i < model.UserSettings.SoundFontPaths.Count; i++)
					liststore.SetValue (liststore.AddRow (), df, model.UserSettings.SoundFontPaths [i]);
			};
			updateSoundFontPaths ();
			model.SoundFontPathsUpdated += updateSoundFontPaths;
			top.PackStart (dirlist, true);
			
			var buttons = new VBox ();
			top.PackStart (buttons);

			var removeDirButton = new Button ("Remove selected directories");
			removeDirButton.Clicked += (o, e) =>
				model.UpdateDirectoryList (model.UserSettings.SoundFontPaths.Except (dirlist.SelectedRows.Select (r => liststore.GetValue (r, df))).ToArray ());
			
			buttons.PackStart (removeDirButton);

			var addDirButton = new Button ("Add new soundfont directory");
			addDirButton.Clicked += (o, e) => RunSoundFontDirectoryChooser ();
			buttons.PackStart (addDirButton);
			
			var filelist = new ListView ();

			return top;
		}

		void RunSoundFontDirectoryChooser ()
		{
			// FIXME: current XWt is buggy and does not respect my `Multiselect = true`.
			// var dlg = new SelectFolderDialog { Title = "Select soundfont directories", Multiselect = true, CanCreateFolders = false };
			var dlg = new SelectFolderDialog { Title = "Select soundfont directories", CanCreateFolders = false };
			if (dlg.Run())
				model.UpdateDirectoryList (model.UserSettings.SoundFontPaths.Concat (dlg.Folders).ToArray ());
		}
	}
}
