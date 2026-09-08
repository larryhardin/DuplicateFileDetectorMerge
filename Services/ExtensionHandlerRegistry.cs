using System;
using System.Collections.Generic;
using System.Linq;

namespace DuplicateFileDetectorMerge.Services
{
    /// <summary>Resolves the appropriate IExtensionHandler for a given file extension. Add new media types by registering a handler here.</summary>
    public static class ExtensionHandlerRegistry
    {
        private static readonly List<IExtensionHandler> _handlers = new()
        {
            new ImageExtensionHandler(),
        };

        public static IExtensionHandler GetHandlerForExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return DefaultExtensionHandler.Instance;
            }

            var normalized = extension.StartsWith('.') ? extension : "." + extension;

            foreach (var handler in _handlers)
            {
                if (handler.SupportedExtensions.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    return handler;
                }
            }

            return DefaultExtensionHandler.Instance;
        }
    }
}
