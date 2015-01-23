﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using IronFoundry.Warden.Containers;
using IronFoundry.Warden.Utilities;

namespace IronFoundry.Container
{
    public class ContainerSpec
    {
        public string Handle { get; set; }
        public BindMount[] BindMounts { get; set; }
        public Dictionary<string, string> Properties { get; set; }
        public Dictionary<string, string> Environment { get; set; }
    }

    public interface IContainerCreationService : IDisposable
    {
        IContainer CreateContainer(ContainerSpec containerSpec);
    }

    public class ContainerCreationService : IContainerCreationService
    {
        readonly string containerBasePath;
        readonly FileSystemManager fileSystem;
        readonly IUserManager userManager;
        readonly ILocalTcpPortManager tcpPortManager;
        readonly IProcessRunner processRunner;
        readonly IContainerHostService containerHostService;

        public ContainerCreationService(
            IUserManager userManager,
            FileSystemManager fileSystem,
            ILocalTcpPortManager tcpPortManager,
            IProcessRunner processRunner,
            IContainerHostService containerHostService,
            string containerBasePath
            )
        {
            this.userManager = userManager;
            this.fileSystem = fileSystem;
            this.tcpPortManager = tcpPortManager;
            this.processRunner = processRunner;
            this.containerHostService = containerHostService;
            this.containerBasePath = containerBasePath;
        }

        public ContainerCreationService(string containerBasePath, string userGroupName)
        {
            var permissionManager = new DesktopPermissionManager();
            this.userManager = new LocalPrincipalManager(permissionManager, userGroupName);
        }

        public IContainer CreateContainer(ContainerSpec containerSpec)
        {
            Guard.NotNull(containerSpec, "containerSpec");

            var handle = containerSpec.Handle;
            if (String.IsNullOrEmpty(handle))
                handle = ContainerHandleGenerator.Generate();

            var user = ContainerUser.Create(userManager, handle);
            var directory = ContainerDirectory.Create(fileSystem, containerBasePath, handle, user);

            return new Container(handle, user, directory, tcpPortManager, processRunner, null);
        }

        public void Dispose()
        {
        }
    }
}
