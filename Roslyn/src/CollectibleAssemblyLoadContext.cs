using System.Reflection;
using System.Runtime.Loader;

namespace Roslyn.Fuzz
{
	internal class CollectibleAssemblyLoadContext : AssemblyLoadContext
	{
		public CollectibleAssemblyLoadContext() : base(true) { }
		protected override Assembly Load(AssemblyName assemblyName) => null;
	}
}
