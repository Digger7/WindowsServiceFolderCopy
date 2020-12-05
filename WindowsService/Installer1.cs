using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace WindowsServicFolderCopy
{
    [RunInstaller(true)]
    public partial class Installer1 : Installer
    {
        ServiceInstaller serviceInstaller;
        ServiceProcessInstaller processInstaller;

        public Installer1()
        {
            InitializeComponent();

            serviceInstaller = new ServiceInstaller();
            processInstaller = new ServiceProcessInstaller();

            processInstaller.Account = ServiceAccount.User;
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "FolderCopyService";
            serviceInstaller.DisplayName = "Folder copy service";
            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
