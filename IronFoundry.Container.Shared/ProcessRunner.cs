﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IronFoundry.Warden.Utilities;

namespace IronFoundry.Container
{
    public class ProcessRunSpec
    {
        static readonly string[] EmptyArguments = new string[0];

        public ProcessRunSpec()
        {
            Environment = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            Arguments = EmptyArguments;
        }

        public string ExecutablePath { get; set; }
        public string[] Arguments { get; set; }
        public Dictionary<string, string> Environment { get; set; }
        public string WorkingDirectory { get; set; }

        public Action<string> OutputCallback { get; set; }
        public Action<string> ErrorCallback { get; set; }
    }

    public interface IProcessRunner
    {
        IProcess Run(ProcessRunSpec runSpec);
    }

    public class ProcessRunner : IProcessRunner
    {
        static readonly string[] EmptyArguments = new string[0];

        readonly ProcessHelper processHelper;

        public ProcessRunner()
            : this(new ProcessHelper())
        {
        }

        public ProcessRunner(ProcessHelper processHelper)
        {
            this.processHelper = processHelper;
        }

        public IProcess Run(ProcessRunSpec runSpec)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = runSpec.ExecutablePath,
                Arguments = String.Join(" ", runSpec.Arguments ?? EmptyArguments),
                WorkingDirectory = runSpec.WorkingDirectory,
                UseShellExecute = false,
                LoadUserProfile = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };

            if (runSpec.Environment != null && runSpec.Environment.Count > 0)
            {
                startInfo.EnvironmentVariables.Clear();
                foreach (var variable in runSpec.Environment)
                {
                    startInfo.EnvironmentVariables[variable.Key] = variable.Value;
                }
            }

            Process p = new Process
            {
                StartInfo = startInfo,
            };

            p.EnableRaisingEvents = true;

            var wrapped = processHelper.WrapProcess(p);

            if (runSpec.OutputCallback != null)
            {
                p.OutputDataReceived += (sender, e) =>
                {
                    runSpec.OutputCallback(e.Data);
                };
            }

            if (runSpec.ErrorCallback != null)
            {
                p.ErrorDataReceived += (sender, e) =>
                {
                    runSpec.ErrorCallback(e.Data);
                };
            }

            bool started = p.Start();
            Debug.Assert(started); // TODO: Should we throw an exception here? Fail fast?

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            return wrapped;
        }
    }
}
