﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IronFoundry.Container.Helpers;
using IronFoundry.Warden.Utilities;
using Xunit;

namespace IronFoundry.Container
{
    public class ProcessRunnerTests
    {
        ProcessRunner Runner { get; set; }

        public ProcessRunnerTests()
        {
            Runner = new ProcessRunner();
        }

        public class Run : ProcessRunnerTests
        {
            static ProcessRunSpec CreateRunSpec(string exePath, string[] arguments = null)
            {
                return new ProcessRunSpec
                {
                    ExecutablePath = exePath,
                    Arguments = arguments,
                };
            }

            static TempFile CreateTempFile()
            {
                return new TempFile(Path.GetTempPath());
            }

            static void WaitForGoodExit(IProcess process)
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new Exception("Test failed because the process failed with exit code: " + process.ExitCode);
            }

            [Fact]
            public void SuppliedArgumentsInStartupInfoIsPassedToProcess()
            {
                using (var tempFile = CreateTempFile())
                {
                    var si = CreateRunSpec("cmd.exe", new [] { "/C", String.Format("echo Boomerang > {0}", tempFile.FullName) });

                    using (var p = Runner.Run(si))
                    {
                        WaitForGoodExit(p);

                        var output = tempFile.ReadAllText();
                        Assert.Contains("Boomerang", output);
                    }
                }
            }

            [Fact]
            public void StartsProcessWithEnvironmentVariables()
            {
                using (var tempFile = CreateTempFile())
                {
                    var si = CreateRunSpec("cmd.exe", new[] { "/C", String.Format(@"set > {0}", tempFile.FullName) });
                    si.Environment["FOO"] = "BAR";
                    si.Environment["FOO2"] = "SNAFU";

                    using (var p = Runner.Run(si))
                    {
                        WaitForGoodExit(p);

                        var output = tempFile.ReadAllText();
                        Assert.Contains("BAR", output);
                        Assert.Contains("SNAFU", output);
                    }
                }
            }

            [Fact]
            public void StartsProcessWithSpecifiedWorkingDirectory()
            {
                using (var tempFile = CreateTempFile())
                {
                    var tempDirectory = tempFile.DirectoryName;
                    var si = CreateRunSpec("cmd.exe", new [] { "/C", String.Format(@"cd > {0}", tempFile.FullName) });
                    si.WorkingDirectory = tempDirectory;

                    using (var p = Runner.Run(si))
                    {
                        WaitForGoodExit(p);

                        var output = tempFile.ReadAllText();
                        Assert.Contains(tempDirectory.TrimEnd('\\'), output);
                    }
                }
            }

            [Fact]
            public void CanGetExitCodeFromCompletedProcess()
            {
                var si = CreateRunSpec("cmd.exe", new[] { "/S", "/C", @"""ping 127.0.0.1 -n 1 && exit""" });

                using (var p = Runner.Run(si))
                {
                    WaitForGoodExit(p);
                    Assert.Equal(0, p.ExitCode);
                }
            }

            [Fact]
            public void WhenProcessExitsWithError_ExitCodeIsCorrect()
            {
                var si = CreateRunSpec("cmd.exe", new[] { "/c", "exit 10" });

                using (var p = Runner.Run(si))
                {
                    p.WaitForExit(2000);
                    Assert.Equal(10, p.ExitCode);
                    p.Kill();
                }
            }

            [Fact]
            public void CanGetStandardOutputFromProcess()
            {
                var spec = CreateRunSpec("cmd.exe", new[] { "/C", "echo This is STDOUT && echo This is STDERR >&2 && pause" });

                var output = new StringBuilder();
                var outputSignal = new ManualResetEvent(false);
                spec.OutputCallback = (data) => 
                {
                    output.Append(data);
                    outputSignal.Set();
                };

                var error = new StringBuilder();
                var errorSignal = new ManualResetEvent(false);
                spec.ErrorCallback = (data) =>
                {
                    error.Append(data);
                    errorSignal.Set();
                };

                using (var p = Runner.Run(spec))
                {
                    try
                    {
                        WaitHandle.WaitAll(new[] { outputSignal, errorSignal }, 1000);
                        Assert.Contains("This is STDOUT", output.ToString());
                        Assert.Contains("This is STDERR", error.ToString());
                    }
                    finally
                    {
                        p.Kill();
                    }
                }
            }

            [Fact]
            public void WhenProcessFailsToStart_ThrowsException()
            {
                var si = CreateRunSpec("DoesNotExist.exe");

                var ex = Assert.Throws<System.ComponentModel.Win32Exception>(() => Runner.Run(si));
            }
        }
    }
}
