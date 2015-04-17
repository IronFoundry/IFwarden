﻿using System;
using System.Threading.Tasks;
using IronFoundry.Warden.Containers;
using IronFoundry.Warden.Jobs;
using NLog;
using IronFoundry.Warden.Properties;
using IronFoundry.Warden.Protocol;

namespace IronFoundry.Warden.Handlers
{
    public class LinkRequestHandler : JobRequestHandler
    {
        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly LinkRequest request;

        public LinkRequestHandler(IContainerManager containerManager, IJobManager jobManager, Request request)
            : base(containerManager, jobManager, request)
        {
            this.request = (LinkRequest)request;
        }

        public override Task<Response> HandleAsync()
        {
            log.Trace("Handle: '{0}' JobId: '{1}'", request.Handle, request.JobId);

            return Task.Run<Response>(async () =>
                {
                    LinkResponse response = null;

                    Job job = jobManager.GetJob(request.JobId);
                    if (job == null)
                    {
                        ResponseData responseData = GetResponseData(true, Resources.JobRequestHandler_NoSuchJob_Message);
                        response = new LinkResponse
                        {
                            ExitStatus = (uint)responseData.ExitStatus,
                            Stderr = responseData.Message,
                        };
                    }
                    else
                    {
                        IJobResult result = await job.RunnableTask;
                        response = new LinkResponse
                        {
                            ExitStatus = (uint)result.ExitCode,
                            Stderr = result.Stderr,
                            Stdout = result.Stdout,
                        };
                    }

                    try
                    {
                        response.Info = await BuildInfoResponseAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // If the container is shutting down the link request may be completed and unable to build an information response
                        // via normal channels.  In this case, we return a stopped info response.
                        response.Info = new InfoResponse() { State = IronFrame.ContainerState.Stopped.ToString() };
                    }
                    catch (Exception)
                    {
                        response.Info = new InfoResponse() { State = IronFrame.ContainerState.Stopped.ToString() };
                    }

                    return response;
                });
        }
    }
}
