using System;
using System.Windows;

namespace Wpf.Fuzz
{
	public static class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			Window win = new Window();
			win.Title = "Fuzzing WPF";

			Application app = new Application();
			app.Run(win);
		}
	}
}
