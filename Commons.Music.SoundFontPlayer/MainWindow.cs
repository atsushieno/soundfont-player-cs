using System;
using System.Linq;
using NAudio.SoundFont;
using Xwt;
using Xwt.WPFBackend;

namespace Commons.Music.SoundFontPlayer
{
	public class MainWindow : Window
	{
		public MainWindow ()
		{
			Width = 800;
			Height = 800;
			this.Closed += (_, __) => Application.Exit ();

			model = new Model ();
			
			SetupWindowContent ();
			
			model.Initialize ();
		}

		Model model;

		void SetupWindowContent ()
		{
			var top = new VBox ();

			top.PackStart (CreateSoundFontFoldersPanel ());

			top.PackStart (CreateSoundFontListPanel (), true);

			/*
			var lv = new ListView ();
			var noteCol = new DataField<int> ();
			var nameCol = new DataField<string> ();
			var store = new ListStore (noteCol, nameCol);
			lv.DataSource = store;
			lv.Columns.Add ("No.", noteCol);
			lv.Columns.Add ("Name", nameCol);
			Action updateSoundFonts = () => {
				store.Clear ();
			};			
			*/
			
			Content = top;
		}
		
		Widget CreateSoundFontListPanel ()
		{
			var sfList =new TreeView () { UseAlternatingRowColors = true, AnimationsEnabled = true };
			var nameCol = new DataField<string> ();
			var bankCol = new DataField<int> ();
			var fileCol = new DataField<string> ();
			var presetCol = new DataField<string> ();
			var namePresetCol = new DataField<string> ();
			var patchCol = new DataField<int> ();
			var progNumCol = new DataField<int> ();
			var instCol = new DataField<string> ();
			var store = new TreeStore (fileCol, nameCol, bankCol, patchCol, presetCol, namePresetCol, progNumCol, instCol);
			sfList.DataSource = store;
			//sfList.Columns.Add ("File", fileCol);
			//sfList.Columns.Add ("Name", nameCol);
			sfList.Columns.Add ("Bank", bankCol);
			sfList.Columns.Add ("Patch", patchCol);
			//sfList.Columns.Add ("Preset", presetCol);
			sfList.Columns.Add ("Name/Preset", namePresetCol);
			sfList.Columns.Add ("Prog.No.", progNumCol);
			sfList.Columns.Add ("Inst", instCol);
			Action updateSoundFonts = () => {
				store.Clear ();
				foreach (var sf in model.LoadedSoundFonts.Where (_ => _.Entity.Value != null)) {
					foreach (var preset in sf.Entity.Value.Presets.OrderBy (p => p.Bank).ThenBy (p => p.PatchNumber)) {
						foreach (var gen in preset.Zones.SelectMany (z => z.Generators.Where (g => g.GeneratorType == GeneratorEnum.Instrument).OrderBy (g => g.HighByteAmount).ThenBy (g => g.LowByteAmount))) {
							var node = store.AddNode ();
							node.SetValues (
								fileCol, sf.FullPath, nameCol,
								sf.Entity.Value.FileInfo.BankName,
								bankCol, preset.Bank,
								patchCol, preset.PatchNumber,
								presetCol, preset.Name,
								namePresetCol, sf.Entity.Value.FileInfo.BankName + " / " + preset.Name,
								progNumCol, gen.HighByteAmount * 0x100 + gen.LowByteAmount,
								instCol, gen.Instrument?.Name);
							node.MoveToLastSibling ();
						}
					}
				}
			};
			model.SoundFontListUpdated += updateSoundFonts;

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
