using System.Diagnostics;
using LocalCloudStorage.ViewModel;
using LocalCloudStorage.Events;

namespace LocalCloudStorage
{
    public class RemoteRequestsViewModel : BaseRequestsViewModel
    {
        public RemoteRequestsViewModel(BufferedRemoteFileStoreInterface remoteFileStoreInterface) : base(remoteFileStoreInterface)
        {
            remoteFileStoreInterface.OnRequestProgressChanged += OnRequestProgressChanged;
        }
        private void OnRequestProgressChanged(object sender, RemoteRequestProgressChangedEventArgs e)
        {
            var request = Requests[e.RequestId];
            if (request == null)
            {
                //well, this is an interesting situation
                Debug.WriteLine("Event to update progress changed on event that does not exist!");
            }
            else
            {
                //update the progress of the item
                request.OnProgressChanged(e);
            }
        }
    }
}
