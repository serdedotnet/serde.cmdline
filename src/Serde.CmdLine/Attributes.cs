
using System;

namespace Serde.CmdLine;

/// <summary>
/// Marks a property or field as an option, such as <c>-v</c> or <c>--output path</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="bool"/> option is a flag and takes no value. Any other option takes the next
/// argument as its value. Options may appear anywhere among the arguments, including after a
/// subcommand or between the values of a collection parameter. After <c>--</c>, nothing is
/// treated as an option.
/// </para>
/// <para>
/// An option is optional if its type is nullable or it has an initializer, and required otherwise.
/// Collection types aren't supported for options yet.
/// </para>
/// </remarks>
/// <param name="flagNames">
/// The option's names, separated by <c>|</c>, for example <c>"-v|--verbose"</c>.
/// </param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false)]
public sealed class CommandOptionAttribute(string flagNames) : Attribute
{
    /// <summary>
    /// The option's names, separated by <c>|</c>, for example <c>"-v|--verbose"</c>.
    /// </summary>
    public string FlagNames { get; } = flagNames;

    /// <summary>
    /// Description of the option, shown in help text.
    /// </summary>
    public string? Description { get; init; } = null;

    /// <summary>
    /// If true, the option is still parseable but omitted from generated help text.
    /// </summary>
    public bool Hidden { get; init; } = false;
}

/// <summary>
/// Marks a property or field as a positional parameter.
/// </summary>
/// <remarks>
/// <para>
/// A parameter is optional if its type is nullable or it has an initializer, and required otherwise.
/// </para>
/// <para>
/// A parameter with a collection type, such as <c>List&lt;string&gt;</c>, takes every remaining
/// positional argument, so it must have the last ordinal. Options may appear between its values.
/// </para>
/// <para>
/// A command can't have both parameters and a <see cref="CommandGroupAttribute"/>.
/// </para>
/// </remarks>
/// <param name="ordinal">The zero-based position of the parameter among the positional arguments.</param>
/// <param name="name">The name shown in help text, as <c>&lt;name&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false)]
public sealed class CommandParameterAttribute(int ordinal, string name) : Attribute
{
    /// <summary>
    /// The zero-based position of the parameter among the positional arguments.
    /// </summary>
    public int Ordinal { get; } = ordinal;

    /// <summary>
    /// The name shown in help text, as <c>&lt;name&gt;</c>.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Description of the parameter, shown in help text.
    /// </summary>
    public string? Description { get; init; } = null;

    /// <summary>
    /// If true, the parameter is still parseable but omitted from generated help text.
    /// </summary>
    public bool Hidden { get; init; } = false;
}

/// <summary>
/// Names a command.
/// </summary>
/// <remarks>
/// On a type, it names the command that type parses, which is used in the usage line of help text.
/// On each case of a <see cref="CommandGroupAttribute"/> type, it names that subcommand. On a
/// property or field, it declares a subcommand that is parsed into that member.
/// </remarks>
/// <param name="name">The command name, as typed on the command line.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class CommandAttribute(string name) : Attribute
{
    /// <summary>
    /// The command name, as typed on the command line.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Short summary of the command.
    /// </summary>
    public string? Summary { get; init; } = null;

    /// <summary>
    /// Detailed description of the command.
    /// </summary>
    public string? Description { get; init; } = null;

    /// <summary>
    /// If true, the command is still parseable but omitted from generated help text.
    /// </summary>
    public bool Hidden { get; init; } = false;
}

/// <summary>
/// Marks a property or field as a choice of subcommands.
/// </summary>
/// <remarks>
/// The member's type is a closed union: an abstract record with a private constructor, whose nested
/// records each have a <see cref="CommandAttribute"/> naming a subcommand. The member is set to the
/// case matching the subcommand given on the command line, or left null if none is given.
/// A command can't have both a command group and <see cref="CommandParameterAttribute"/> parameters.
/// </remarks>
/// <param name="name">The name shown in the usage line of help text, as <c>&lt;name&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false)]
public sealed class CommandGroupAttribute(string name) : Attribute
{
    /// <summary>
    /// The name shown in the usage line of help text, as <c>&lt;name&gt;</c>.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// If true, the command group is still parseable but omitted from generated help text.
    /// </summary>
    public bool Hidden { get; init; } = false;
}