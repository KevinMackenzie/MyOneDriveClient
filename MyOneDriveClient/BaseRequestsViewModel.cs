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
                        //the request might have a different type, so go by ID

                        var activeReq = (from req in ActiveRequests where req.RequestId == request.RequestId select req).First();
                        if (activeReq != null)
                        {
                            ActiveRequests.Remove(activeReq);
                        }
                    }
                    break;
                case FileStoreRequest.RequestStatus.WaitForUser:
                    if (request != null)
                    {
                        //it will ALWAYS be the top item TODO: will it?
                        if (Enum.TryParse(e.ErrorMessage, out FileStoreInterface.UserPrompts prompt))
                        {
                            switch (prompt)
                            {
                                case FileStoreInterface.UserPrompts.KeepOverwriteOrRename:
                                    ActiveRequests[0] = new AwaitUserRequestViewModel(request);
                                    break;
                                case FileStoreInterface.UserPrompts.CloseApplication:
                                    ActiveRequests[0] = new CloseAppRequestViewModel(request);
                                    break;
                                case FileStoreInterface.UserPrompts.Acknowledge:
                                    ActiveRequests[0] =
                                        new AcknowledgeErrorRequestViewModel(request, "No Error Message Given");
                                    break;
                            }
                        }
                        else
                        {
                            ActiveRequests[0] = new AcknowledgeErrorRequestViewModel(request, e.ErrorMessage);
                        }

                        //ActiveRequests.Remove(request);
                        //AwaitUserRequests.Add(request);
                    }
                    break;
                //case FileStoreRequest.RequestStatus.Failure:
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
