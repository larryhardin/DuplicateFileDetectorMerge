using System.Collections.Generic;
using System.IO;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using DuplicateFileDetectorMerge.Models;

namespace DuplicateFileDetectorMerge.Services
{
    /// <summary>Handles image files: falls back to perceptual-hash similarity when sizes differ slightly.</summary>
    public sealed class ImageExtensionHandler : IExtensionHandler
    {
        private static readonly PerceptualHash _hasher = new();

        public string MediaTypeName => "Image";

        // Extensions decodable by ImageSharp's built-in decoders, and therefore supported by PerceptualHash.
        public IReadOnlyCollection<string> SupportedExtensions { get; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".pbm", ".cur", ".jpeg", ".jpg", ".tiff", ".tif", ".gif",
            ".exr", ".tga", ".ani", ".webp", ".ico", ".png", ".qoi"
        };

        public bool AreSimilar(ListElement fromElement, ListElement toElement)
        {
            var fileSizeRatio = (double)toElement.FileSize / fromElement.FileSize;

            // Arbitrary tolerance: metadata can differ slightly while the image content is still identical.
            if (fileSizeRatio <= 0.999 || fileSizeRatio >= 1.001)
            {
                return false;
            }

            ComparisonActivity.RaiseStarted(fromElement, toElement);
            try
            {
                using var stream1 = File.OpenRead(Path.Combine(fromElement.Path, fromElement.FileName + fromElement.Extension));
                using var stream2 = File.OpenRead(Path.Combine(toElement.Path, toElement.FileName + toElement.Extension));

                var hash1 = _hasher.Hash(stream1);
                var hash2 = _hasher.Hash(stream2);

                return CompareHash.Similarity(hash1, hash2) >= 95.0;
            }
            finally
            {
                ComparisonActivity.RaiseEnded();
            }
        }
    }
}
