using System.Collections.Generic;
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
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.ListBoxItem: return Convert(element.ListBoxItem);
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.ListBox: return Convert(element.ListBox);
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.TreeViewItem: return Convert(element.TreeViewItem);
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.TreeView: return Convert(element.TreeView);
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

		private static ListBoxItem Convert(Layout.ListBoxItem listBoxItem)
		{
			return new ListBoxItem { Content = Convert(listBoxItem.Content) };
		}

		private static ListBox Convert(Layout.ListBox listBox)
		{
			var result = new ListBox();

			foreach (var child in listBox.Children)
			{
				if (Convert(child) is FrameworkElement element)
				{
					result.Items.Add(element);
				}
			}

			return result;
		}

		private static TreeViewItem Convert(Layout.TreeViewItem treeViewItem)
		{
			var result = new TreeViewItem();

			foreach (var child in treeViewItem.Children)
			{
				if (Convert(child) is FrameworkElement element)
				{
					result.Items.Add(element);
				}
			}

			return result;
		}

		private static TreeView Convert(Layout.TreeView treeView)
		{
			var result = new TreeView();

			foreach (var child in treeView.Children)
			{
				if (Convert(child) is FrameworkElement element)
				{
					result.Items.Add(element);
				}
			}

			return result;
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
