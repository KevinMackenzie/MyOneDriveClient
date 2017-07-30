using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    public class BaseRequestsViewModel
    {
        protected ConcurrentDictionary<int, FileStoreRequestViewModel> Requests { get; } = new ConcurrentDictionary<int, FileStoreRequestViewModel>();

        //private ObservableCollection<AwaitUserRequestViewModel> _awaitUserRequests = new ObservableCollection<AwaitUserRequestViewModel>();
        //private ObservableCollection<FileStoreRequestViewModel> _failedRequests = new ObservableCollection<FileStoreRequestViewModel>();

        public BaseRequestsViewModel(FileStoreInterface fileStoreInterface)
        {
            fileStoreInterface.OnRequestStatusChanged += OnRequestStatusChanged;
        }
        private void OnRequestStatusChanged(object sender, RequestStatusChangedEventArgs e)
        {
            Requests.TryGetValue(e.RequestId, out var request);

            switch (e.Status)
            {
                case FileStoreRequest.RequestStatus.Success:
                case FileStoreRequest.RequestStatus.Cancelled:
                    if (!Requests.TryRemove(e.RequestId, out request)) return;

                    if (request != null)
                    {
                        ActiveRequests.Remove(request);
                    }
                    break;
                case FileStoreRequest.RequestStatus.WaitForUser:
                    if (request != null)
                    {
                        ActiveRequests.Remove(request);
                        AwaitUserRequests.Add(request);
                    }
                    break;
                case FileStoreRequest.RequestStatus.Failure:
                    if (request != null)
                    {
                        ActiveRequests.Remove(request);
                        FailedRequests.Add(request);
                    }
                    break;
                default:
                    if (request == null)
                    {
                        request = Requests[e.RequestId] =
                            new FileStoreRequestViewModel(e.Path,
                                e.Type, e.Status,
                                e.RequestId);

                        ActiveRequests.Add(request);
                    }
                    else
                    {
                        //TODO: this throws an exception when called from the worker thread
                        request.OnStatusChanged(e.Status);
                    }
                    break;
            }
        }


        public ObservableCollection<FileStoreRequestViewModel> ActiveRequests { get; } = new ObservableCollection<FileStoreRequestViewModel>();
        public ObservableCollection<FileStoreRequestViewModel> AwaitUserRequests { get; } = new ObservableCollection<FileStoreRequestViewModel>();
        public ObservableCollection<FileStoreRequestViewModel> FailedRequests { get; } = new ObservableCollection<FileStoreRequestViewModel>();
    }
}
