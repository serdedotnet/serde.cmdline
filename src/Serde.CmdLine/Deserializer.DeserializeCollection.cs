namespace Serde.CmdLine;

internal sealed partial class Deserializer
{
    /// <summary>
    /// Reads a variadic parameter: every remaining positional arg in the current command.
    /// Options may appear between the values, as in getopt. They are recorded as skipped options
    /// and read by the command that owns them once the args run out.
    /// </summary>
    private sealed class DeserializeCollection(
        Deserializer deserializer,
        Command _command
    ) : TypeDeserializerBase(deserializer)
    {
        private int _count = 0;

        public override int TryReadIndex(ISerdeInfo info)
        {
            ref int argIndex = ref _deserializer._argIndex;
            string[] args = _deserializer._args;
            while (argIndex < args.Length)
            {
                if (_deserializer.TryConsumeSpecialArg(_command.Info))
                {
                    continue;
                }

                var arg = args[argIndex];
                if (!_deserializer.IsOption(arg))
                {
                    return _count++;
                }

                // The current command is the top of the stack, so its options are checked too.
                if (!_deserializer.TrySkipOption(_deserializer._commandStack.Count - 1, _deserializer._skippedOptions))
                {
                    throw new ArgumentSyntaxException($"Unexpected argument: '{arg}'");
                }
            }
            return ITypeDeserializer.EndOfType;
        }

        public override void End(ISerdeInfo info) { }
    }
}
