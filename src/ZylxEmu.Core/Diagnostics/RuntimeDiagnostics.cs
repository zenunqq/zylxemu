using System;
using System.Diagnostics;
using ZylxEmu.Logging;

namespace ZylxEmu.Core.Diagnostics
{
    // Lightweight runtime diagnostics helper to add more trace points without touching core logic.
    // This file is safe to include and helps capture loader/VM/HLE/shader events during startup.
    public static class RuntimeDiagnostics
    {
        private static readonly ZylxEmuLogger _log = new ZylxEmuLogger("RuntimeDiagnostics");

        public static void LogLoaderInfo(string message)
        {
            _log.Log(LogLevel.Trace, "Loader: " + message);
        }

        public static void LogMemoryInfo(string message)
        {
            _log.Log(LogLevel.Trace, "VirtualMemory: " + message);
        }

        public static void LogHleInfo(string message)
        {
            _log.Log(LogLevel.Trace, "HLE: " + message);
        }

        public static void LogShaderInfo(string message)
        {
            _log.Log(LogLevel.Trace, "Shader: " + message);
        }

        public static void LogMediaInfo(string message)
        {
            _log.Log(LogLevel.Trace, "Media: " + message);
        }

        public static void LogException(string where, Exception ex)
        {
            _log.Log(LogLevel.Error, $"Exception in {where}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
        }

        public static void FailFast(string why)
        {
            _log.Log(LogLevel.Error, "FailFast: " + why);
            Debug.Fail(why);
        }
    }
}
