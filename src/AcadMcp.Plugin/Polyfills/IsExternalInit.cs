// Polyfill so 'record' and init-only properties work on net48.
// Compiled only for NETFRAMEWORK; on net8.0+ this type ships in BCL.

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
