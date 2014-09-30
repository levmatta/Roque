namespace Cinchcast.Roque.Common
{
    using System;
    using System.Linq;

    /// <summary>
    /// Work service interface example
    /// </summary>
    public interface ITrace
    {
        void TraceVerbose(string format, params object[] arguments);
        void TraceInformation(string format, params object[] arguments);
        void TraceError(string format, params object[] arguments);
        void TraceWarning(string format, params object[] arguments);
    }
}
