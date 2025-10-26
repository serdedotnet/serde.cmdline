# Serde.CmdLine Benchmarks

This project contains benchmarks for the Serde.CmdLine library using BenchmarkDotNet.

## Running the Benchmarks

To run all benchmarks:

```bash
dotnet run -c Release
```

To run specific benchmarks (e.g., only parsing benchmarks):

```bash
dotnet run -c Release --filter "*Parse*"
```

## Benchmark Scenarios

The benchmark suite includes the following scenarios:

### Parsing Benchmarks

- **ParseSimpleCommand**: Parses a simple command with minimal options (boolean flag + string parameter)
- **ParseComplexCommand**: Parses a more complex command with multiple option types (string options, boolean flags, positional parameters)
- **ParseSubCommand**: Parses a command with subcommands and nested options
- **ParseWithHelp**: Parses command line arguments that trigger help text generation
- **ParseManyOptions**: Parses a command with many different options

### Help Generation Benchmarks

- **GenerateHelpText**: Generates help text for a standard command
- **GenerateSubCommandHelpText**: Generates help text for a command with subcommands

## Understanding Results

BenchmarkDotNet will provide:
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Gen0/Gen1/Gen2**: Garbage collection counts per 1000 operations
- **Allocated**: Total memory allocated per operation

Lower values are better for all metrics.

## Customizing Benchmarks

The benchmark configurations can be customized by modifying the attributes on the `CmdLineBenchmarks` class:

- `[MemoryDiagnoser]`: Enables memory allocation tracking
- `[SimpleJob(RuntimeMoniker.Net90)]`: Specifies the runtime to benchmark against

You can add additional runtimes, job configurations, or exporters as needed.
