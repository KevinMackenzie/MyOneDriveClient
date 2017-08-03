using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalCloudStorage.Encryption
{
    /// <summary>
    /// Provides a common interface for encryption purposes
    /// </summary>
    public interface IEncryptionProvider
    {
        /// <summary>
        /// Synchronously encrypts a set of data
        /// </summary>
        /// <param name="buffer">the data to encrypt</param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        void EncryptData(byte[] buffer, int offset, int count);
        void DecryptData(byte[] buffer, int offset, int count);
        /// <summary>
        /// Asynchronously encrypts a set of data
        /// </summary>
        /// <param name="buffer">the data to encrypt</param>
        /// <returns></returns>
        //Task EncryptDataAsync(byte[] buffer, int offset, int count);
    }
}
