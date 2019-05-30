// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;

namespace AspNetCore.Fuzz
{
	public static partial class HttpUtilities
	{
		public static bool IsHostHeaderValid(string hostText)
		{
			if (string.IsNullOrEmpty(hostText))
			{
				// The spec allows empty values
				return true;
			}

			var firstChar = hostText[0];
			if (firstChar == '[')
			{
				// Tail call
				return IsIPv6HostValid(hostText);
			}
			else
			{
				if (firstChar == ':')
				{
					// Only a port
					return false;
				}

				var invalid = HttpCharacters.IndexOfInvalidHostChar(hostText);
				if (invalid >= 0)
				{
					// Tail call
					return IsHostPortValid(hostText, invalid);
				}

				return true;
			}
		}

		// The lead '[' was already checked
		private static bool IsIPv6HostValid(string hostText)
		{
			for (var i = 1; i < hostText.Length; i++)
			{
				var ch = hostText[i];
				if (ch == ']')
				{
					// [::1] is the shortest valid IPv6 host
					if (i < 4)
					{
						return false;
					}
					else if (i + 1 < hostText.Length)
					{
						// Tail call
						return IsHostPortValid(hostText, i + 1);
					}
					return true;
				}

				if (!IsHex(ch) && ch != ':' && ch != '.')
				{
					return false;
				}
			}

			// Must contain a ']'
			return false;
		}

		private static bool IsHostPortValid(string hostText, int offset)
		{
			var firstChar = hostText[offset];
			offset++;
			if (firstChar != ':' || offset == hostText.Length)
			{
				// Must have at least one number after the colon if present.
				return false;
			}

			for (var i = offset; i < hostText.Length; i++)
			{
				if (!IsNumeric(hostText[i]))
				{
					return false;
				}
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsNumeric(char ch)
		{
			// '0' <= ch && ch <= '9'
			// (uint)(ch - '0') <= (uint)('9' - '0')

			// Subtract start of range '0'
			// Cast to uint to change negative numbers to large numbers
			// Check if less than 10 representing chars '0' - '9'
			return (uint)(ch - '0') < 10u;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsHex(char ch)
		{
			return IsNumeric(ch)
				// || ('a' <= ch && ch <= 'f')
				// || ('A' <= ch && ch <= 'F');

				// Lowercase indiscriminately (or with 32)
				// Subtract start of range 'a'
				// Cast to uint to change negative numbers to large numbers
				// Check if less than 6 representing chars 'a' - 'f'
				|| (uint)((ch | 32) - 'a') < 6u;
		}
	}
}
