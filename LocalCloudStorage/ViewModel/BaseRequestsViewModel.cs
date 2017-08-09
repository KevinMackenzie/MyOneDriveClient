using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using LocalCloudStorage.Events;

namespace LocalCloudStorage.ViewModel
{
    public class BaseRequestsViewModel
    {
        protected ConcurrentDictionary<int, FileStoreRequestViewModel> Requests { get; } = new ConcurrentDictionary<int, FileStoreRequestViewModel>();

        //private ObservableCollection<AwaitUserRequestViewModel> _awaitUserRequests = new ObservableCollection<AwaitUserRequestViewModel>();
        //private ObservableCollection<FileStoreRequestViewModel> _failedRequests = new ObservableCollection<FileStoreRequestViewModel>();

        private int IndexOf(IFileStoreRequestIdentifiable request)
        {
            var activeReq = (from req in ActiveRequests where req.RequestId == request.RequestId select req).First();
            if (activeReq == null) return -1;
            return ActiveRequests.IndexOf(activeReq);
        }

        public BaseRequestsViewModel(FileStoreInterface fileStoreInterface)
        {
            fileStoreInterface.OnRequestStatusChanged += OnRequestStatusChanged;
        }
        private void OnRequestStatusChanged(object sender, RequestStatusChangedEventArgs e)
        {
            Requests.TryGetValue(e.RequestId, out var request);

            switch (e.Status)
            {
                case RequestStatus.Success:
                case RequestStatus.Cancelled:
                    if (!Requests.TryRemove(e.RequestId, out request)) return;

                    if (request != null)
                    {
                        //the request might have a different type, so go by ID

                        var activeReqIndex = IndexOf(request);
                        if (activeReqIndex != -1)
                        {
                            ActiveRequests.RemoveAt(activeReqIndex);
                        }
                    }
                    break;
                case RequestStatus.WaitForUser:
                    if (request != null)
                    {
                        var requestPosition = IndexOf(request);
                        if (requestPosition == -1)
                        {
                            Debug.WriteLine("Request callback received, but does not exist in request list!");
                            return;
                        }

                        if (Enum.TryParse(e.ErrorMessage, out FileStoreInterface.UserPrompts prompt))
                        {
                            switch (prompt)
                            {
                                case FileStoreInterface.UserPrompts.KeepOverwriteOrRename:
                                    ActiveRequests[requestPosition] = new AwaitUserRequestViewModel(request);
                                    break;
                                case FileStoreInterface.UserPrompts.CloseApplication:
                                    ActiveRequests[requestPosition] = new CloseAppRequestViewModel(request);
                                    break;
                                case FileStoreInterface.UserPrompts.Acknowledge:
                                    ActiveRequests[requestPosition] =
                                        new AcknowledgeErrorRequestViewModel(request, "No Error Message Given");
                                    break;
                            }
                        }
                        else
                        {
                            ActiveRequests[requestPosition] = new AcknowledgeErrorRequestViewModel(request, e.ErrorMessage);
                        }

                        //ActiveRequests.Remove(request);
                        //AwaitUserRequests.Add(request);
                    }
                    break;
                //case RequestStatus.Failure:
                //    if (request != null)
                //    {
                //        //it will ALWAYS be the top item
                //        ActiveRequests[0] = new AcknowledgeErrorRequestViewModel(request, e.ErrorMessage);
                //
                //        //ActiveRequests.Remove(request);
                //        //FailedRequests.Add(request);
                //    }
                //    break;
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
                        ActiveRequests[0] = request; //TODO: is this always safe?
                    }
                    break;
            }
        }


        public ObservableCollection<FileStoreRequestViewModelBase> ActiveRequests { get; } = new ObservableCollection<FileStoreRequestViewModelBase>();
    }
}
