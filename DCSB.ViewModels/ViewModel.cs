﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using DCSB.Business;
using DCSB.Input;
using DCSB.Models;
using DCSB.Utils;
using System.Threading.Tasks;
using DCSB.Colors;

namespace DCSB.ViewModels
{
    public class ViewModel : ObservableObject
    {
        private ApplicationStateModel _applicationStateModel;
        private ConfigurationModel _configurationModel;

        private ConfigurationManager _configurationManager;
        private OpenFileManager _openFileManager;
        private ShortcutManager _shortcutManager;
        private SoundManager _soundManager;
        private UpdateManager _updateManager;
        private KeyboardInput _keyboardInput;
        private PaletteManager _paletteManager;

        private PresetConfigurationViewModel _presetConfigurationViewModel;

        private double _previousVolume;
        private double _previousPrimaryVolume;
        private double _previousSecondaryVolume;

        public ViewModel()
        {
            Swatches = new SwatchesProvider().Swatches;

            _applicationStateModel = new ApplicationStateModel();
            _configurationManager = new ConfigurationManager();
            _configurationModel = _configurationManager.Load();
            if (_configurationModel.PresetCollection.Count == 0) _configurationModel.PresetCollection.Add(new Preset() { Name = "New Preset" } );
            _openFileManager = new OpenFileManager();

            _soundManager = new SoundManager(_configurationModel.PrimaryOutputDevice, _configurationModel.SecondaryOutputDevice)
            {
                Volume = _configurationModel.Volume / 100f,
                PrimaryDeviceVolume = _configurationModel.PrimaryDeviceVolume / 100f,
                SecondaryDeviceVolume = _configurationModel.SecondaryDeviceVolume / 100f,
                Overlap = _configurationModel.Overlap
            };
            _shortcutManager = new ShortcutManager(_applicationStateModel, _configurationModel, _soundManager);
            _updateManager = new UpdateManager();
            _paletteManager = new PaletteManager();

            _presetConfigurationViewModel = new PresetConfigurationViewModel(_applicationStateModel, _configurationModel);

            _configurationModel.PropertyChanged += (sender, e) => _configurationManager.Save((ConfigurationModel)sender);

            _configurationModel.CounterShortcuts.Next.Command = NextCounterCommand;
            _configurationModel.CounterShortcuts.Previous.Command = PreviousCounterCommand;
            _configurationModel.CounterShortcuts.Increment.Command = IncrementCommand;
            _configurationModel.CounterShortcuts.Decrement.Command = DecrementCommand;
            _configurationModel.CounterShortcuts.Reset.Command = ResetCommand;
            _configurationModel.SoundShortcuts.Pause.Command = PauseCommand;
            _configurationModel.SoundShortcuts.Continue.Command = ContinueCommand;
            _configurationModel.SoundShortcuts.Stop.Command = StopCommand;

            Task.Run(() => _updateManager.AutoUpdateCheck(Version));
        }

        public IntPtr WindowHandle
        {
            set
            {
                _keyboardInput = new KeyboardInput(value);
                _keyboardInput.KeyUp += _shortcutManager.KeyUp;
                _keyboardInput.KeyDown += _shortcutManager.KeyDown;
                _keyboardInput.KeyPress += _shortcutManager.KeyPress;
            }
        }

        public ApplicationStateModel ApplicationStateModel
        {
            get { return _applicationStateModel; }
        }

        public ConfigurationModel ConfigurationModel
        {
            get { return _configurationModel; }
        }

        public PresetConfigurationViewModel PresetConfigurationViewModel
        {
            get { return _presetConfigurationViewModel; }
        }

        public IEnumerable<Swatch> Swatches { get; }

        public GridLength CountersWidth
        {
            get { return new GridLength(_configurationModel.CountersWidth, GridUnitType.Star); }
            set
            {
                _configurationModel.CountersWidth = value.Value;
                RaisePropertyChanged("CountersWidth");
            }
        }

        public GridLength SoundsWidth
        {
            get { return new GridLength(_configurationModel.SoundsWidth, GridUnitType.Star); }
            set
            {
                _configurationModel.SoundsWidth = value.Value;
                RaisePropertyChanged("SoundsWidth");
            }
        }

        public Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public double CurrentVolume
        {
            get { return _configurationModel.Volume; }
            set
            {
                _configurationModel.Volume = (int)value;
                _soundManager.Volume = _configurationModel.Volume / 100f;
                RaisePropertyChanged("CurrentVolume");
            }
        }

        public double PrimaryDeviceVolume
        {
            get { return _configurationModel.PrimaryDeviceVolume; }
            set
            {
                _configurationModel.PrimaryDeviceVolume = (int)value;
                _soundManager.PrimaryDeviceVolume = _configurationModel.PrimaryDeviceVolume / 100f;
                RaisePropertyChanged("PrimaryDeviceVolume");
            }
        }

        public double SecondaryDeviceVolume
        {
            get { return _configurationModel.SecondaryDeviceVolume; }
            set
            {
                _configurationModel.SecondaryDeviceVolume = (int)value;
                _soundManager.SecondaryDeviceVolume = _configurationModel.SecondaryDeviceVolume / 100f;
                RaisePropertyChanged("SecondaryDeviceVolume");
            }
        }

        public bool Overlap
        {
            get { return _configurationModel.Overlap; }
            set
            {
                _configurationModel.Overlap = value;
                _soundManager.Overlap = _configurationModel.Overlap;
                RaisePropertyChanged("Overlap");
            }
        }

        public DisplayOption Enable
        {
            get { return _configurationModel.Enable; }
            set
            {
                _configurationModel.Enable = value;
                RaisePropertyChanged("Enable");
                if (_configurationModel.Enable != DisplayOption.Sounds && _configurationModel.Enable != DisplayOption.Both)
                {
                    _soundManager.Stop();
                }
            }
        }

        public IList<OutputDevice> AvailableOutputDevices
        {
            get { return _soundManager.EnumerateDevices(); }
        }

        public IList<OutputDevice> SecondaryOutputDevices
        {
            get
            {
                IList<OutputDevice> devices = AvailableOutputDevices;
                devices.Insert(0, new OutputDevice(-2, "Disabled"));
                return devices;
            }
        }

        public int PrimaryOutputDeviceIndex
        {
            get
            {
                IList<OutputDevice> devices = AvailableOutputDevices;
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i].Number == _configurationModel.PrimaryOutputDevice.Number)
                    {
                        return i;
                    }
                }
                return 0;
            }
            set
            {
                _configurationModel.PrimaryOutputDevice = AvailableOutputDevices[value];
                _soundManager.ChangePrimaryDevice(_configurationModel.PrimaryOutputDevice);
                RaisePropertyChanged("PrimaryOutputDevice");
            }
        }

        public int SecondaryOutputDeviceIndex
        {
            get
            {
                IList<OutputDevice> devices = SecondaryOutputDevices;
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i].Number == _configurationModel.SecondaryOutputDevice.Number)
                    {
                        return i;
                    }
                }
                return 0;
            }
            set
            {
                _configurationModel.SecondaryOutputDevice = SecondaryOutputDevices[value];
                _soundManager.ChangeSecondaryDevice(_configurationModel.SecondaryOutputDevice);
                RaisePropertyChanged("SecondaryOutputDevice");
            }
        }

        public ICommand CheckForUpdatesCommand
        {
            get { return new RelayCommand(CheckForUpdates); }
        }
        private async void CheckForUpdates()
        {
            await _updateManager.ManualUpdateCheck(Version);
        }

        public ICommand PresetSelectedCommand
        {
            get { return new RelayCommand<Preset>(PresetSelected); }
        }
        private void PresetSelected(Preset selectedPreset)
        {
            _configurationModel.SelectedPreset = selectedPreset;
            foreach (Counter counter in selectedPreset.CounterCollection)
            {
                counter.ReadFromFile();
            }
        }

        public ICommand OpenSettingsCommand
        {
            get { return new RelayCommand(OpenSettings); }
        }
        private void OpenSettings()
        {
            _applicationStateModel.SettingsOpened = true;
        }

        public ICommand OpenCounterCommand
        {
            get { return new RelayCommand(OpenCounter, AreCountersEnabled); }
        }
        private void OpenCounter()
        {
            if (_configurationModel.SelectedPreset.SelectedCounter != null)
            {
                _applicationStateModel.ModifiedCounter = _configurationModel.SelectedPreset.SelectedCounter;
                _applicationStateModel.CounterOpened = true;
            }
        }

        public ICommand OpenSoundCommand
        {
            get { return new RelayCommand(OpenSound, AreSoundsEnabled); }
        }
        private void OpenSound()
        {
            if (_configurationModel.SelectedPreset.SelectedSound != null)
            {
                _applicationStateModel.ModifiedSound = _configurationModel.SelectedPreset.SelectedSound;
                _applicationStateModel.SoundOpened = true;
            }
        }

        public ICommand OpenAboutCommand
        {
            get { return new RelayCommand(OpenAbout); }
        }
        private void OpenAbout()
        {
            _applicationStateModel.AboutOpened = true;
        }

        public ICommand OpenCounterFileDialogCommand
        {
            get { return new RelayCommand(OpenCounterFileDialog, AreCountersEnabled); }
        }
        private void OpenCounterFileDialog()
        {
            string result = _openFileManager.OpenCounterFile();
            if (result != null)
            {
                _applicationStateModel.ModifiedCounter.File = result;
            }
        }

        public ICommand OpenSoundFileDialogCommand
        {
            get { return new RelayCommand(OpenSoundFileDialog, AreSoundsEnabled); }
        }
        private void OpenSoundFileDialog()
        {
            string[] result = _openFileManager.OpenSoundFiles();
            if (result != null)
            {
                _configurationModel.SelectedPreset.SelectedSound.Files.Clear();
                foreach (string file in result)
                {
                    _applicationStateModel.ModifiedSound.Files.Add(file);
                }
            }
        }

        public ICommand AddCounterCommand
        {
            get { return new RelayCommand(AddCounter, AreCountersEnabled); }
        }
        private void AddCounter()
        {
            Counter counter = new Counter();
            _configurationModel.SelectedPreset.CounterCollection.Add(counter);
            _configurationModel.SelectedPreset.SelectedCounter = counter;
            _applicationStateModel.ModifiedCounter = counter;
            _applicationStateModel.CounterOpened = true;
        }

        public ICommand RemoveCounterCommand
        {
            get { return new RelayCommand(RemoveCounter, AreCountersEnabled); }
        }
        private void RemoveCounter()
        {
            if (_configurationModel.SelectedPreset.SelectedCounter != null)
            {
                _configurationModel.SelectedPreset.CounterCollection.Remove(_configurationModel.SelectedPreset.SelectedCounter);
            }
        }

        public ICommand IncrementCommand
        {
            get { return new RelayCommand(Increment, AreCountersEnabled); }
        }
        private void Increment()
        {
            if (_configurationModel.SelectedPreset.SelectedCounter != null)
            {
                _configurationModel.SelectedPreset.SelectedCounter.Count += _configurationModel.SelectedPreset.SelectedCounter.Increment;
            }
        }

        public ICommand DecrementCommand
        {
            get { return new RelayCommand(Decrement, AreCountersEnabled); }
        }
        private void Decrement()
        {
            if (_configurationModel.SelectedPreset.SelectedCounter != null)
            {
                _configurationModel.SelectedPreset.SelectedCounter.Count -= _configurationModel.SelectedPreset.SelectedCounter.Increment;
            }
        }

        public ICommand ResetCommand
        {
            get { return new RelayCommand(Reset, AreCountersEnabled); }
        }
        private void Reset()
        {
            if (_configurationModel.SelectedPreset.SelectedCounter != null)
            {
                _configurationModel.SelectedPreset.SelectedCounter.Count = 0;
            }
        }

        public ICommand NextCounterCommand
        {
            get { return new RelayCommand(NextCounter, AreCountersEnabled); }
        }
        private void NextCounter()
        {
            if (_configurationModel.SelectedPreset.SelectedCounter == null )
            {
                if (_configurationModel.SelectedPreset.CounterCollection.Count != 0)
                {
                    _configurationModel.SelectedPreset.SelectedCounter = _configurationModel.SelectedPreset.CounterCollection[0];
                }
            }
            else
            {
                int currentIndex = _configurationModel.SelectedPreset.CounterCollection.IndexOf(_configurationModel.SelectedPreset.SelectedCounter);
                int nextIndex = (currentIndex + 1) % _configurationModel.SelectedPreset.CounterCollection.Count;
                _configurationModel.SelectedPreset.SelectedCounter = _configurationModel.SelectedPreset.CounterCollection[nextIndex];
            }
        }

        public ICommand PreviousCounterCommand
        {
            get { return new RelayCommand(PreviousCounter, AreCountersEnabled); }
        }
        private void PreviousCounter()
        {
            if (_configurationModel.SelectedPreset.SelectedCounter == null)
            {
                if (_configurationModel.SelectedPreset.CounterCollection.Count != 0)
                {
                    _configurationModel.SelectedPreset.SelectedCounter = _configurationModel.SelectedPreset.CounterCollection[0];
                }
            }
            else
            {
                int currentIndex = _configurationModel.SelectedPreset.CounterCollection.IndexOf(_configurationModel.SelectedPreset.SelectedCounter);
                int previousIndex = (currentIndex - 1 + _configurationModel.SelectedPreset.CounterCollection.Count) % _configurationModel.SelectedPreset.CounterCollection.Count;
                _configurationModel.SelectedPreset.SelectedCounter = _configurationModel.SelectedPreset.CounterCollection[previousIndex];
            }
        }

        public ICommand MuteCommand
        {
            get { return new RelayCommand(Mute); }
        }
        private void Mute()
        {
            if (CurrentVolume == 0)
            {
                CurrentVolume = _previousVolume;
            }
            else
            {
                _previousVolume = CurrentVolume;
                CurrentVolume = 0;
            }
        }

        public ICommand MutePrimaryCommand
        {
            get { return new RelayCommand(MutePrimary); }
        }
        private void MutePrimary()
        {
            if (PrimaryDeviceVolume == 0)
            {
                PrimaryDeviceVolume = _previousPrimaryVolume;
            }
            else
            {
                _previousPrimaryVolume = PrimaryDeviceVolume;
                PrimaryDeviceVolume = 0;
            }
        }

        public ICommand MuteSecondaryCommand
        {
            get { return new RelayCommand(MuteSecondary); }
        }
        private void MuteSecondary()
        {
            if (SecondaryDeviceVolume == 0)
            {
                SecondaryDeviceVolume = _previousSecondaryVolume;
            }
            else
            {
                _previousSecondaryVolume = SecondaryDeviceVolume;
                SecondaryDeviceVolume = 0;
            }
        }

        public ICommand AddSoundCommand
        {
            get { return new RelayCommand(AddSound, AreSoundsEnabled); }
        }
        private void AddSound()
        {
            Sound sound = new Sound();
            _configurationModel.SelectedPreset.SelectedSound = sound;
            _configurationModel.SelectedPreset.SoundCollection.Add(sound);
            _applicationStateModel.ModifiedSound = sound;
            _applicationStateModel.SoundOpened = true;
        }

        public ICommand RemoveSoundCommand
        {
            get { return new RelayCommand(RemoveSound, AreSoundsEnabled); }
        }
        private void RemoveSound()
        {
            _configurationModel.SelectedPreset.SoundCollection.Remove(_configurationModel.SelectedPreset.SelectedSound);
        }

        public ICommand PlayCommand
        {
            get { return new RelayCommand(Play, AreSoundsEnabled); }
        }
        private void Play()
        {
            if (_configurationModel.SelectedPreset.SelectedSound != null)
            {
                _soundManager.Play(_configurationModel.SelectedPreset.SelectedSound);
            }
        }

        public ICommand PauseCommand
        {
            get { return new RelayCommand(Pause, AreSoundsEnabled); }
        }
        private void Pause()
        {
            _soundManager.Pause();
        }

        public ICommand ContinueCommand
        {
            get { return new RelayCommand(Continue, AreSoundsEnabled); }
        }
        private void Continue()
        {
            _soundManager.Continue();
        }

        public ICommand StopCommand
        {
            get { return new RelayCommand(Stop, AreSoundsEnabled); }
        }
        private void Stop()
        {
            _soundManager.Stop();
        }

        public ICommand BindKeysCommand
        {
            get { return new RelayCommand<IBindable>(BindKeys); }
        }
        public void BindKeys(IBindable bindable)
        {
            _applicationStateModel.ModifiedBindable = bindable;
            _applicationStateModel.BindKeysOpened = true;
        }

        public ICommand CancelBindKeysCommand
        {
            get { return new RelayCommand(CancelBindKeys); }
        }
        public void CancelBindKeys()
        {
            _applicationStateModel.BindKeysOpened = false;
            _applicationStateModel.ModifiedBindable = null;
        }

        public ICommand ClearKeysCommand
        {
            get { return new RelayCommand(ClearKeys); }
        }
        private void ClearKeys()
        {
            _applicationStateModel.BindKeysOpened = false;
            _applicationStateModel.ModifiedBindable.Keys.Clear();
            _applicationStateModel.ModifiedBindable = null;
        }

        public ICommand ToggleBaseCommand
        {
            get { return new RelayCommand<bool>(ApplyBase); }
        }

        private void ApplyBase(bool isDark)
        {
            _paletteManager.SetLightDark(isDark);
        }

        public ICommand ClosingCommand
        {
            get { return new RelayCommand(Closing); }
        }
        private void Closing()
        {
            _configurationManager.Dispose();
        }

        private bool AreCountersEnabled()
        {
            return _configurationModel.Enable == DisplayOption.Counters || _configurationModel.Enable == DisplayOption.Both;
        }

        private bool AreSoundsEnabled()
        {
            return _configurationModel.Enable == DisplayOption.Sounds || _configurationModel.Enable == DisplayOption.Both;
        }
    }
}
