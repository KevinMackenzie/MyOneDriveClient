namespace LocalCloudStorage
{
    /// <summary>
    /// Creates a remote file store connection for a given type
    /// </summary>
    public interface IRemoteFileStoreConnectionFactory
    {
        /// <summary>
        /// Creates a new instance of the connection.  This instance must
        ///  be ready for <see cref="IConnection.LogUserInAsync"/>
        ///  to be called
        /// </summary>
        /// <param name="cacheLocation">The location of the cache files for
        ///  whatever this connection needs to cache.  This will be unique
        ///  for each call of <see cref="Construct"/>, so unique path generation
        ///  is handled by the caller</param>
        /// <returns>The initialized connection instance</returns>
        IConnection Construct(string cacheLocation);

        string ServiceName { get; }

        /// <summary>
        /// Whether this kind of <see cref="IConnection"/> needs its own
        ///  special kind of <see cref="IRemoteFileStoreInterface"/>
        /// </summary>
        //bool OverridesFileStoreInterface { get; }
        /// <summary>
        /// The special kind of interface that is needed to run this <see cref="IConnection"/>
        /// </summary>
        /// <returns>a new instance of a specialized <see cref="IRemoteFileStoreInterface"/> or null if not applicable</returns>
        //IRemoteFileStoreInterface ConstructInterface();
    }
}
