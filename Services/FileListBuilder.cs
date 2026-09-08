using System;
using System.Collections.Generic;
using System.IO;
using DuplicateFileDetectorMerge.Models;

namespace DuplicateFileDetectorMerge.Services
{
    /// <summary>Builds a sorted list of ListElement entries for every file under a root folder.</summary>
    public static class FileListBuilder
    {
        public static List<ListElement> BuildList(string rootFolder)
        {
            var elements = new List<ListElement>();

            foreach (var filePath in Directory.EnumerateFiles(rootFolder, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(filePath);
                elements.Add(new ListElement
                {
                    Path = info.DirectoryName ?? string.Empty,
                    FileName = System.IO.Path.GetFileNameWithoutExtension(info.Name),
                    Extension = info.Extension,
                    FileSize = info.Length,
                    DateCreated = info.CreationTime,
                    DateModified = info.LastWriteTime
                });
            }

            elements.Sort();
            return elements;
        }
    }
}
