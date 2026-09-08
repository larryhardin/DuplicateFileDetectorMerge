using System;

namespace DuplicateFileDetectorMerge.Models
{
    /// <summary>
    /// Represents a single file entry within a compare list.
    /// Sort order is based on a hash of FileName + Extension; the key is not required to be unique.
    /// </summary>
    public class ListElement : IComparable<ListElement>
    {
        public string Path { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }

        /// <summary>Sortable hash used to order the list; collisions are allowed.</summary>
        public int SortKey => ComputeSortKey(FileName, Extension);

        public static int ComputeSortKey(string fileName, string extension)
        {
            return HashCode.Combine(
                fileName?.ToLowerInvariant() ?? string.Empty,
                extension?.ToLowerInvariant() ?? string.Empty);
        }

        public string FullPath => System.IO.Path.Combine(Path, FileName + Extension);

        public int CompareTo(ListElement? other)
        {
            if (other is null) return 1;
            return SortKey.CompareTo(other.SortKey);
        }

        public override string ToString()
        {
            return $"{FileName}{Extension} | Size={FileSize} | Created={DateCreated} | Modified={DateModified} | Path={Path}";
        }
    }
}
