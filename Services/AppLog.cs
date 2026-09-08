using System;

namespace DuplicateFileDetectorMerge.Services
{
    /// <summary>Decouples log message producers (handlers, comparison logic) from the UI that displays them.</summary>
    public static class AppLog
    {
        public static event Action<string>? MessageLogged;

        public static void Log(string message) => MessageLogged?.Invoke(message);
    }
}
