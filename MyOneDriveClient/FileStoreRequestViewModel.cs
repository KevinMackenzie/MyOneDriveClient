using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;
using LocalCloudStorage;

namespace MyOneDriveClient
{
    public class FileStoreRequestViewModel : FileStoreRequestViewModelBase
    {
        private long _progress;
        private FileStoreRequest.RequestStatus _status;
        private long _total;

        public FileStoreRequestViewModel(string path, FileStoreRequest.RequestType type, FileStoreRequest.RequestStatus status, int requestId) : base(requestId)
        {
            Path = path;
            Type = type;
            _status = status;
            Percent = 0;
        }

        public void OnStatusChanged(FileStoreRequest.RequestStatus newStatus)
        {
            Status = newStatus;
        }
        public void OnProgressChanged(ProgressChangedEventArgs e)
        {
            _total = e.Total;
            _progress = e.Complete;
            Percent = e.Progress;
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(Percent));
        }

        #region Properties
        public string Path { get; }
        public FileStoreRequest.RequestType Type { get; }
        public string Progress
        {
            get
            {
                switch (Type)
                {
                    case FileStoreRequest.RequestType.Write:
                        return $"Uploading ... {_progress} out of {_total} bytes";
                    case FileStoreRequest.RequestType.Read:
                        return $"Downloading ... {_progress} out of {_total} bytes";
                }
                return $"";
            }
        }
        public double Percent { get; private set; }
        public FileStoreRequest.RequestStatus Status
        {
            get => _status;
            private set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}
