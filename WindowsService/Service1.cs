using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;

namespace WindowsServiceGuard

{
    public partial class Service1 : ServiceBase
    {
        Сopyist copyist;
        public Service1()
        {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            copyist = new Сopyist();
            Thread loggerThread = new Thread(new ThreadStart(copyist.Start));
            loggerThread.Start();
        }

        protected override void OnStop()
        {

        }
    }

    class Сopyist
    {
        public Сopyist()
        {

        }

        public void Start()
        {
            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            aTimer.Interval = 5000;
            aTimer.Enabled = true;
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            string[] dirs = Directory.GetFiles(@"\\RONPP-S-FS10\video$", "*");
            string list = "";
            foreach (string dir in dirs)
            {
                list += dir + Environment.NewLine;
            }

            File.WriteAllText("c:\\!del\\templog.txt", list);
        }
    }
}
