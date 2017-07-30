using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    public class FileStoreRequestViewModel : ViewModelBase
    {
        private long _progress;
        private double _percent;
        private FileStoreRequest.RequestStatus _status;
        private long _total;

        public FileStoreRequestViewModel(string path, FileStoreRequest.RequestType type, FileStoreRequest.RequestStatus status, int requestId)
        {
            Path = path;
            Type = type;
            _status = status;
            RequestId = requestId;
        }

        public void OnStatusChanged(FileStoreRequest.RequestStatus newStatus)
        {
            Status = newStatus;
        }
        public void OnProgressChanged(ProgressChangedEventArgs e)
        {
            _total = e.Total;
            _progress = e.Complete;
            _percent = e.Progress * 100.0;
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(Percent));
        }

        #region Properties
        public int RequestId { get; }
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
