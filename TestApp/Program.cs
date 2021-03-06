﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        class TestItem
        {
            public int Value1 { get; set; }
            public int Value2 { get; set; }
        }

        static ManualResetEvent done = new ManualResetEvent(false);
        static void Main(string[] args)
        {
            /*WebClient client = new WebClient();
            client.UploadProgressChanged += new UploadProgressChangedEventHandler(client_UploadProgressChanged);
            client.UploadFileCompleted += new UploadFileCompletedEventHandler(client_UploadFileCompleted);
            client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
            client.DownloadFileCompleted += Client_DownloadFileCompleted; ;
            //client.UploadFileAsync(new Uri("http://localhost/upload"), "C:\\test.zip");

            //done.WaitOne();
            done.Reset();

            client.DownloadFileAsync(new Uri("http://releases.ubuntu.com/16.04.2/ubuntu-16.04.2-desktop-amd64.iso"), "C:\\Users\\kjmac\\Downloads\\test.iso");


            done.WaitOne();


            Console.WriteLine("Done");*/

            ConcurrentDictionary<int, TestItem> testItems = new ConcurrentDictionary<int, TestItem>();
            testItems.TryAdd(0, new TestItem{Value1 = 1, Value2 = 1});

            Console.WriteLine($"Value1: {testItems[0].Value1}, Value1: {testItems[0].Value2}");

            var item = testItems[0];
            item.Value1 = 5;
            item.Value2 = 9;

            Console.WriteLine($"Value1: {testItems[0].Value1}, Value1: {testItems[0].Value2}");


            Console.Read();
        }

        /*static void Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            done.Set();
        }

        static void client_UploadFileCompleted(object sender, UploadFileCompletedEventArgs e)
        {
            done.Set();
        }

        static void client_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            Console.Write("\rUploading: {0}%  {1} of {2}", e.ProgressPercentage, e.BytesSent, e.TotalBytesToSend);
        }

        static void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine($"Downloading: {e.ProgressPercentage}%  {e.BytesReceived} of {e.TotalBytesToReceive}");
        }*/
    }
}
