//------------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using System.IO;
using System.Security.Cryptography;
using Microsoft.Identity.Client;

namespace LocalCloudStorage.OneDrive
{
    internal class TokenCacheHelper
    {
        public TokenCacheHelper(string cacheFilePath)
        {
            //= System.Reflection.Assembly.GetExecutingAssembly().Location + "msalcache.txt";
            CacheFilePath = cacheFilePath;
        }
 
        /// <summary>
        /// Get the user token cache
        /// </summary>
        /// <returns></returns>
        public TokenCache GetUserCache()
        {
            if (_usertokenCache == null)
            {
                _usertokenCache = new TokenCache();
                _usertokenCache.SetBeforeAccess(BeforeAccessNotification);
                _usertokenCache.SetAfterAccess(AfterAccessNotification);
            }
            return _usertokenCache;
        }

        TokenCache _usertokenCache;

        /// <summary>
        /// Path to the token cache
        /// </summary>
        public string CacheFilePath { get; }

        private readonly object FileLock = new object();

        public void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                args.TokenCache.Deserialize(File.Exists(CacheFilePath)
                    ? File.ReadAllBytes(CacheFilePath)
                    : null);
            }
        }

        public void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (args.TokenCache.HasStateChanged)
            {
                lock (FileLock)
                {
                    // reflect changesgs in the persistent store
                    File.WriteAllBytes(CacheFilePath, args.TokenCache.Serialize());
                    // once the write operationtakes place restore the HasStateChanged bit to filse
                    args.TokenCache.HasStateChanged = false;
                }
            }
        }
    }
}
