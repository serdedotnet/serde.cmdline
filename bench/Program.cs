using BenchmarkDotNet.Running;
using Serde.CmdLine.Benchmarks;

BenchmarkRunner.Run<CmdLineBenchmarks>(args: args);
