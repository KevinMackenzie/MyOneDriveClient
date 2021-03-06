﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalCloudStorage.ViewModel;
using LocalCloudStorage.Composition;
using LocalCloudStorage.Model;
using Newtonsoft.Json;

namespace LocalCloudStorage.ViewModel
{
    public class LocalCloudStorageAppViewModel : ViewModelBase, IDisposable
    {

        #region Private Fields
        private static string _configFileName = "instances.config";
        private static string _pluginsFolderName = "Plugins";
        private static string _tokenCacheFolderName = "TokenCache";

        private string _workingDirectory;
        private RemoteConnectionFactoryManager _factoryManager;
        private LocalCloudStorageData _instancesData;

        private LocalCloudStorageViewModel _localCloudStorage;
        private RemoteFileStoreConnectionFactoriesViewModel _remoteConnectionFactories;
        #endregion

        /// <summary>
        /// Creates a new instance of the <see cref="LocalCloudStorageApp"/>
        /// </summary>
        /// <param name="workingDirectory">the directory the app should work out of</param>
        public LocalCloudStorageAppViewModel(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            _factoryManager = new RemoteConnectionFactoryManager();
            _remoteConnectionFactories = new RemoteFileStoreConnectionFactoriesViewModel(_factoryManager);
        }

        #region Private Methods
        #endregion

        #region Public Control Methods
        public void PrepWorkingDirectory()
        {
            //TODO: do any work to make sure the other control methods won't throw dumb exceptions
            if (!Directory.Exists(_workingDirectory))
            {
                Directory.CreateDirectory(_workingDirectory);
            }

            //This gets rid of any garbage in the path (like ../ escapes)
            var directoryData = new DirectoryInfo(_workingDirectory);
            _workingDirectory = directoryData.FullName;

            if (!Directory.Exists(PluginsFolderPath))
            {
                Directory.CreateDirectory(PluginsFolderPath);
            }

            if (!Directory.Exists(TokenCacheFolderPath))
            {
                Directory.CreateDirectory(TokenCacheFolderPath);
            }
        }
        public async Task LoadInstances()
        {
            Debug.WriteLine("Loading instances from the disc...");
            //remove existing instances
            LocalCloudStorage?.Dispose();
            

            if (!File.Exists(ConfigFilePath))
            {
                Debug.WriteLine("Instance configurations do not exist, creating blank ...");
                _instancesData = new LocalCloudStorageData();
                LocalCloudStorage = new LocalCloudStorageViewModel(_instancesData, _factoryManager, TokenCacheFolderPath);
                await SaveInstances();
                return;
            }

            try
            {
                string jsonText;
                using (var reader = new StreamReader(ConfigFilePath))
                {
                    jsonText = await reader.ReadToEndAsync();
                }
                _instancesData = JsonConvert.DeserializeObject<LocalCloudStorageData>(jsonText);
                if (_instancesData == null)
                {
                    Debug.WriteLine("Instance configurations were corrupt, creating blank ...");
                    _instancesData = new LocalCloudStorageData();
                    LocalCloudStorage = new LocalCloudStorageViewModel(_instancesData, _factoryManager, TokenCacheFolderPath);
                    await SaveInstances();
                }
                else
                {
                    LocalCloudStorage = new LocalCloudStorageViewModel(_instancesData, _factoryManager, TokenCacheFolderPath);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to load instance configurations, creating blank ...");
                Utils.LogException(e);
                _instancesData = new LocalCloudStorageData();
                LocalCloudStorage = new LocalCloudStorageViewModel(_instancesData, _factoryManager, TokenCacheFolderPath);
                await SaveInstances();
            }
        }
        public async Task SaveInstances()
        {
            Debug.WriteLine("Saving instances to the disc...");

            //don't save if they don't exist
            if (LocalCloudStorage == null)
            {
                Debug.WriteLine($"Instances not yet loaded.  Call {nameof(LoadInstances)} first.");
                return;
            }

            try
            {
                using (var writer = new StreamWriter(ConfigFilePath))
                {
                    await writer.WriteAsync(JsonConvert.SerializeObject(_instancesData, Formatting.Indented));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to save instance configurations ...");
                Utils.LogException(e);
            }
        }
        public void ScanForPlugins()
        {
            _factoryManager.ImportFactories(PluginsFolderPath);
        }
        #endregion

        #region Public Properties
        public string ConfigFilePath => $"{_workingDirectory}/{_configFileName}";
        public string PluginsFolderPath => $"{_workingDirectory}/{_pluginsFolderName}";
        public string TokenCacheFolderPath => $"{_workingDirectory}/{_tokenCacheFolderName}";
        public string WorkingDirectory => _workingDirectory;
        #endregion

        #region Major Public Properties
        public LocalCloudStorageViewModel LocalCloudStorage
        {
            get { return _localCloudStorage; }
            private set
            {
                _localCloudStorage = value;
                OnPropertyChanged();
            }
        }
        public RemoteFileStoreConnectionFactoriesViewModel RemoteConnectionFactories
        {
            get { return _remoteConnectionFactories; }
            private set
            {
                _remoteConnectionFactories = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Debugging/Logging
        public DebugLogViewModel DebugLog { get; } = new DebugLogViewModel();
        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            LocalCloudStorage?.Dispose();
        }
    }
}
