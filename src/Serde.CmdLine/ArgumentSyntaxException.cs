// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Serde.CmdLine;

/// <summary>
/// Thrown when the command-line arguments don't match the command definition, for example an
/// unknown option or a missing required option. The message is suitable to show to the user.
/// </summary>
public sealed class ArgumentSyntaxException : Exception
{
    /// <summary>
    /// Creates an exception with a message to show to the user.
    /// </summary>
    public ArgumentSyntaxException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates an exception with a message to show to the user, wrapping the error that caused it.
    /// </summary>
    public ArgumentSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}