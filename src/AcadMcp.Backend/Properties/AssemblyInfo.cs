// Exposes internal types (palettes, patterns, tag-prefix tables) to the xUnit test
// project so unit tests can assert on per-discipline data without making those
// types public just for testability.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AcadMcp.Tests")]
