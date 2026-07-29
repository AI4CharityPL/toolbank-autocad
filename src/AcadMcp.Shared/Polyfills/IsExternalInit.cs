// Polyfill for `record` and `init`-only properties on net48.
// Removed automatically on net8.0+ via conditional compilation.

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal static class IsExternalInit { }
#endif
