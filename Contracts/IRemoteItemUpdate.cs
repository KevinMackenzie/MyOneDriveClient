namespace LocalCloudStorage
{
    public interface IRemoteItemUpdate
    {
        /// <summary>
        /// Whether this the file was deleted from the server
        /// </summary>
        bool Deleted { get; }

        /// <summary>
        /// A handle to the updated item.  Null if <see cref="Deleted" is true/>
        /// </summary>
        IRemoteItemHandle ItemHandle { get; }
    }
}
