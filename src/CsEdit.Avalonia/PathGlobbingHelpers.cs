
// Copyright 2021 Ryan Crosby
// This code is licensed under the MIT license

// https://gist.github.com/crozone/9a10156a37c978e098e43d800c6141ad 
// https://stackoverflow.com/questions/188892/glob-pattern-matching-in-net 

using System;
using System.Text;
using System.Text.RegularExpressions;

// ...

public static class PathGlobbingHelpers
{
    /// <summary>
    /// Checks if a path with globbing wildcards ('?', '*', '**') matches a specified path.
    /// Also works with filenames if only a filename is provided as the path. In this case, dirSeparatorChars can be a zero length span, and '*' will behave the same as '**'.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <param name="pattern">The glob pattern. May include the wildcards '?', '*', '**'.</param>
    /// <param name="dirSeparatorChars">Directory separator characters that are valid in the path. This changes the matching behaviour of '*'.</param>
    /// <returns></returns>
    public static bool PathMatchesGlob(string path, string pattern, ReadOnlySpan<char> dirSeparatorChars)
    {
        return Regex.Match(path, GlobbedPathToRegex(pattern, dirSeparatorChars)).Success;
    }

    /// <summary>
    /// Converts a glob pattern into an equivalent regex pattern, which can then be matched against a path.
    /// </summary>
    /// <param name="pattern">The glob pattern</param>
    /// <param name="dirSeparatorChars">Directory separator characters that are valid in the path. This changes the matching behaviour of '*'.</param>
    /// <returns></returns>
    public static string GlobbedPathToRegex(ReadOnlySpan<char> pattern, ReadOnlySpan<char> dirSeparatorChars)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append('^');

        ReadOnlySpan<char> remainder = pattern;

        while (remainder.Length > 0)
        {
            int specialCharIndex = remainder.IndexOfAny('*', '?');

            if (specialCharIndex >= 0)
            {
                ReadOnlySpan<char> segment = remainder.Slice(0, specialCharIndex);

                if (segment.Length > 0)
                {
                    string escapedSegment = Regex.Escape(segment.ToString());
                    builder.Append(escapedSegment);
                }

                char currentCharacter = remainder[specialCharIndex];
                char nextCharacter = specialCharIndex < remainder.Length - 1 ? remainder[specialCharIndex + 1] : '\0';

                switch (currentCharacter)
                {
                    case '*':
                        if (nextCharacter == '*')
                        {
                            // We have a ** glob expression
                            // Match any character, 0 or more times.
                            builder.Append("(.*)");

                            // Skip over **
                            remainder = remainder.Slice(specialCharIndex + 2);
                        }
                        else
                        {
                            // We have a * glob expression
                            // Match any character that isn't a dirSeparatorChar, 0 or more times.
                            if(dirSeparatorChars.Length > 0) {
                                builder.Append($"([^{Regex.Escape(dirSeparatorChars.ToString())}]*)");
                            }
                            else {
                                builder.Append("(.*)");
                            }

                            // Skip over *
                            remainder = remainder.Slice(specialCharIndex + 1);
                        }
                        break;
                    case '?':
                        builder.Append("(.)"); // Regex equivalent of ?

                        // Skip over ?
                        remainder = remainder.Slice(specialCharIndex + 1);
                        break;
                }
            }
            else
            {
                // No more special characters, append the rest of the string
                string escapedSegment = Regex.Escape(remainder.ToString());
                builder.Append(escapedSegment);
                remainder = ReadOnlySpan<char>.Empty;
            }
        }

        builder.Append('$');

        return builder.ToString();
    }
}

