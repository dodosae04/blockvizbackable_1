// BlockViz.Applications/Helpers/IntExtensions.cs
namespace BlockViz.Applications
{
    public static class IntExtensions
    {
        public static int Mod(this int x, int m) => (x % m + m) % m;
    }
}