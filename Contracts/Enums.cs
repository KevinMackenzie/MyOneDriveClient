namespace LocalCloudStorage
{
    public enum RequestStatus
    {
        /// <summary>
        /// Request was successfuly completed
        /// </summary>
        Success,
        /// <summary>
        /// Request is in queue/await network connection
        /// </summary>
        Pending,
        /// <summary>
        /// Request is being processed
        /// </summary>
        InProgress,
        /// <summary>
        /// Request did not successfully complete
        /// </summary>
        //Failure,
        /// <summary>
        /// Request was cancelled
        /// </summary>
        Cancelled,
        /// <summary>
        /// Request is waiting for user input
        /// </summary>
        WaitForUser
    }

    public enum RequestType
    {
        Write,
        Read,
        Rename,
        Move,
        Create,
        Delete
    }

    public enum UserPrompts
    {
        KeepOverwriteOrRename,
        CloseApplication,
        Acknowledge
    }

    public enum ConflictResolutions
    {
        KeepLocal,
        KeepRemote,
        KeepBoth
    }
}
