using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using YoutubeExplode.Bridge.Cipher;
using YoutubeExplode.Utils;
using YoutubeExplode.Utils.Extensions;

namespace YoutubeExplode.Bridge;

internal partial class PlayerSource
{
    private readonly string _content;

    public CipherManifest? CipherManifest => Memo.Cache(this, () =>
    {
        // Extract the signature timestamp
        var signatureTimestamp = Regex.Match(_content, @"(?:signatureTimestamp|sts):(\d{5})")
            .Groups[1]
            .Value
            .NullIfWhiteSpace();

        if (string.IsNullOrWhiteSpace(signatureTimestamp))
            return null;

        // Find where the player calls the cipher functions
        var cipherCallsite = Regex.Match(
            _content,
            """
            [$_\w]+=function\([$_\w]+\){([$_\w]+)=\1\.split\(['"]{2}\);.*?return \1\.join\(['"]{2}\)}
            """,
            RegexOptions.Singleline
        ).Groups[0].Value.NullIfWhiteSpace();

        if (string.IsNullOrWhiteSpace(cipherCallsite))
            return null;

        // Find the object that defines the cipher functions
        var cipherContainerName = Regex.Match(cipherCallsite, @"([$_\w]+)\.[$_\w]+\([$_\w]+,\d+\);")
            .Groups[1]
            .Value;

        if (string.IsNullOrWhiteSpace(cipherContainerName))
            return null;

        // Find the definition of the cipher functions
        var cipherDefinition = Regex.Match(
            _content,
            $$"""
            var {{Regex.Escape(cipherContainerName)}}={.*?};
            """,
            RegexOptions.Singleline
        ).Groups[0].Value.NullIfWhiteSpace();

        if (string.IsNullOrWhiteSpace(cipherDefinition))
            return null;

        // Identify the swap cipher function
        var swapFuncName = Regex.Match(
            cipherDefinition,
            @"([$_\w]+):function\([$_\w]+,[$_\w]+\){+[^}]*?%[^}]*?}",
            RegexOptions.Singleline
        ).Groups[1].Value.NullIfWhiteSpace();

        // Identify the splice cipher function
        var spliceFuncName = Regex.Match(
            cipherDefinition,
            @"([$_\w]+):function\([$_\w]+,[$_\w]+\){+[^}]*?splice[^}]*?}",
            RegexOptions.Singleline
        ).Groups[1].Value.NullIfWhiteSpace();

        // Identify the reverse cipher function
        var reverseFuncName = Regex.Match(
            cipherDefinition,
            @"([$_\w]+):function\([$_\w]+\){+[^}]*?reverse[^}]*?}",
            RegexOptions.Singleline
        ).Groups[1].Value.NullIfWhiteSpace();

        var operations = new List<ICipherOperation>();

        foreach (var statement in cipherCallsite.Split(';'))
        {
            var calledFuncName = Regex.Match(statement, @"[$_\w]+\.([$_\w]+)\([$_\w]+,\d+\)").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(calledFuncName))
                continue;

            if (string.Equals(calledFuncName, swapFuncName, StringComparison.Ordinal))
            {
                var index = Regex.Match(statement, @"\([$_\w]+,(\d+)\)").Groups[1].Value.ParseInt();
                operations.Add(new SwapCipherOperation(index));
            }
            else if (string.Equals(calledFuncName, spliceFuncName, StringComparison.Ordinal))
            {
                var index = Regex.Match(statement, @"\([$_\w]+,(\d+)\)").Groups[1].Value.ParseInt();
                operations.Add(new SpliceCipherOperation(index));
            }
            else if (string.Equals(calledFuncName, reverseFuncName, StringComparison.Ordinal))
            {
                operations.Add(new ReverseCipherOperation());
            }
        }

        return new CipherManifest(signatureTimestamp, operations);
    });

    public PlayerSource(string content) => _content = content;
}

internal partial class PlayerSource
{
    public static PlayerSource Parse(string raw) => new(raw);
}