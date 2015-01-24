﻿using System;
using IronFoundry.Container.Messages;
using IronFoundry.Warden.Utilities;

namespace IronFoundry.Container
{
    public class ConstrainedProcessRunner : IProcessRunner, IDisposable
    {
        readonly IContainerHostClient hostClient;

        public ConstrainedProcessRunner(IContainerHostClient hostClient)
        {
            this.hostClient = hostClient;
        }

        Action<ProcessDataEvent> BuildProcessDataCallback(Action<string> outputCallback, Action<string> errorCallback)
        {
            return (processData) =>
            {
                switch (processData.dataType)
                {
                    case ProcessDataType.STDOUT:
                        if (outputCallback != null)
                            outputCallback(processData.data);
                        break;

                    case ProcessDataType.STDERR:
                        if (errorCallback != null)
                            errorCallback(processData.data);
                        break;
                }
            };
        }

        public void Dispose()
        {
            if (hostClient != null)
                hostClient.Dispose();
        }

        public IProcess Run(ProcessRunSpec runSpec)
        {
            Guid processKey = Guid.NewGuid();
            
            var defaultEnvironmentBlock = EnvironmentBlock.GenerateDefault();

            CreateProcessParams @params = new CreateProcessParams
            {
                key = processKey,
                executablePath = runSpec.ExecutablePath,
                arguments = runSpec.Arguments,
                environment = defaultEnvironmentBlock.Merge(runSpec.Environment).ToDictionary(),
                workingDirectory = runSpec.WorkingDirectory
            };

            var processDataCallback = BuildProcessDataCallback(runSpec.OutputCallback, runSpec.ErrorCallback);

            hostClient.SubscribeToProcessData(processKey, processDataCallback);

            var result = hostClient.CreateProcess(@params);
            var process = new ConstrainedProcess(hostClient, processKey, result.id);

            return process;
        }
    }
}