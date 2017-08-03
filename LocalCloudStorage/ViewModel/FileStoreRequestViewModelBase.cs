namespace LocalCloudStorage.ViewModel
{
    public abstract class FileStoreRequestViewModelBase : ViewModelBase, IFileStoreRequestIdentifiable
    {
        public FileStoreRequestViewModelBase(int requestId)
        {
            RequestId = requestId;
        }
        public int RequestId { get; }
    }
}
