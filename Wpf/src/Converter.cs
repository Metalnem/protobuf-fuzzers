using System.Windows;
using System.Windows.Controls;

namespace Wpf.Fuzz
{
	internal static class Converter
	{
		public static FrameworkElement Convert(Layout.FrameworkElement element)
		{
			if (element == null)
			{
				return null;
			}

			switch (element.FrameworkelementOneofCase)
			{
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.TextBlock: return Convert(element.TextBlock);
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.TextBox: return Convert(element.TextBox);
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.Button: return Convert(element.Button);
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.StackPanel: return Convert(element.StackPanel);
			}

			return null;
		}

		private static TextBlock Convert(Layout.TextBlock textBlock)
		{
			return new TextBlock { Text = textBlock.Text };
		}

		private static TextBox Convert(Layout.TextBox textBox)
		{
			return new TextBox { Text = textBox.Text };
		}

		private static Button Convert(Layout.Button button)
		{
			return new Button { Content = Convert(button.Content) };
		}

		private static StackPanel Convert(Layout.StackPanel stackPanel)
		{
			var result = new StackPanel();

			if (stackPanel.Orientation == Layout.Orientation.Vertical)
			{
				result.Orientation = Orientation.Vertical;
			}
			else
			{
				result.Orientation = Orientation.Horizontal;
			}

			foreach (var child in stackPanel.Children)
			{
				if (Convert(child) is FrameworkElement element)
				{
					result.Children.Add(element);
				}
			}

			return result;
		}
	}
}
