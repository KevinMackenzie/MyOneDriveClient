﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    static class PathUtils
    {
        public static string GetParentItemPath(string path)
        {
            var parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return "/";

            string ret = "";
            for (int i = 0; i < parts.Length - 1; ++i)
            {
                ret = $"{ret}/{parts[i]}";
            }
            return ret;
        }

        public static string GetItemName(string path)
        {
            return path.Split(new char[] { '/' }).Last();
        }

        public static string CompileString(IEnumerable<string> pathParts, string name)
        {
            var path = "";
            foreach (var part in pathParts)
            {
                path += $"/{part}";
            }

            return $"{path}/{name}";
        }

        public static string GetRenamedPath(string oldPath, string newName)
        {
            return $"{PathUtils.GetParentItemPath(oldPath)}/{newName}";
        }

        /// <summary>
        /// Inserts a given string between the name and file extension of a given path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="toInsert"></param>
        /// <returns></returns>
        public static string InsertString(string path, string toInsert)
        {
            var pathParts = path.Split('/');
            var name = pathParts.Last();

            var nameParts = name.Split('.');
            var namePreExt = nameParts.First();

            namePreExt += toInsert;

            var ret = "/";
            for (int i = 0; i < pathParts.Length - 1; ++i)
            {
                ret = $"{ret}{pathParts[i]}/";
            }
            ret = $"{ret}/{namePreExt}";
            for (int i = 1; i < nameParts.Length; ++i)
            {
                ret = $"{ret}.{nameParts}";
            }
            return ret;
        }
    }
}
