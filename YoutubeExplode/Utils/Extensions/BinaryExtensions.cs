﻿using System.Text;

namespace YoutubeExplode.Utils.Extensions;

internal static class BinaryExtensions
{
    public static string ToHex(this byte[] data, bool isUpperCase = true)
    {
        var buffer = new StringBuilder(2 * data.Length);

        foreach (var b in data)
        {
            buffer.Append(
                b.ToString(isUpperCase ? "X2" : "x2")
            );
        }

        return buffer.ToString();
    }
}