open System.Diagnostics
open System

// Waits for the process to finish, if the return code is not zero, prints a message and terminates
let WaitAndCheck (proc : Process) =
    proc.WaitForExit()
    if proc.ExitCode <> 0 then
        Console.WriteLine("Error: process failed")
        exit(-1)

if not Environment.Is64BitOperatingSystem then
    Console.WriteLine("Error: only 64 bit OS supported")
    exit -1

let baseName = IO.DirectoryInfo(Environment.CurrentDirectory).Name
let (rid, exeName) =
    if OperatingSystem.IsLinux() then "linux-x64", baseName
    elif OperatingSystem.IsWindows() then "win-x64", baseName + ".exe"
    else "osx-x64", baseName

// Publish a binary with AOT compilation, so there is little to no JIT overhead polluting the benchmark
Process.Start("dotnet",
    $"publish -c Release -r {rid} --no-self-contained -p:PublishReadyToRun=true -o bin/Benchmark")
|> WaitAndCheck

// Check if we should attach a profiler
if fsi.CommandLineArgs.Length = 1 then
    Process.Start($"bin/Benchmark/{exeName}") |> WaitAndCheck
else
    if "--profile" <> fsi.CommandLineArgs.[1] then
        Console.WriteLine($"Error: Unknown argument '{fsi.CommandLineArgs.[1]}'")
        exit(-1)

    // Make sure dotnet-trace is available as a global tool
    Process.Start("dotnet", "tool install --global dotnet-trace")
        .WaitForExit()
    Process.Start("dotnet-trace", $"collect --format speedscope -- bin/Benchmark/{exeName}")
    |> WaitAndCheck
