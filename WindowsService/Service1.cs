using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
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

        private static SqlConnection Connection()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database.mdf"); ;
            return new SqlConnection($"Data Source = (LocalDB)\\MSSQLLocalDB; AttachDbFilename = {path}; Integrated Security = True");
        }

        public void Start()
        {
            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            aTimer.Interval = Convert.ToDouble(GetSettingValue("Interval"));
            aTimer.Enabled = true;
        }

        private object GetSettingValue(string name)
        {
            try
            {
                using (var conn = Connection())
                {
                    conn.Open();
                    SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT Value FROM Settings WHERE Name = @Name";
                    cmd.Parameters.Add("@Name", SqlDbType.NVarChar).Value = name;
                    SqlDataReader myReader = cmd.ExecuteReader();
                    string _result = "";
                    while (myReader.Read())
                    {
                        _result = myReader["Value"].ToString();
                    }
                    return _result;
                }
            }
            catch (Exception ex)
            {
                //File.WriteAllText("c:\\!del\\templog.txt", ex.Message);
                return null;
            }
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            #region example
            //string[] dirs = Directory.GetFiles(@"\\RONPP-S-FS10\video$", "*");
            //string list = "";
            //foreach (string dir in dirs)
            //{
            //    list += dir + Environment.NewLine;
            //}
            //File.WriteAllText("c:\\!del\\templog.txt", list);


            //try
            //{
            //    using (var conn = Connection())
            //    {
            //        conn.Open();
            //        SqlCommand cmd = conn.CreateCommand();
            //        cmd.CommandText = "SELECT Id, Source FROM Source WHERE Id <> @Id";
            //        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = 0;
            //        SqlDataReader myReader = cmd.ExecuteReader();
            //        string _result = "";
            //        while (myReader.Read())
            //        {
            //            _result += String.Format("Id: {0}; Source: {1} | ", myReader["Id"].ToString(), myReader["Source"].ToString())+Environment.NewLine;
            //        }
            //        File.WriteAllText("c:\\!del\\templog.txt", _result);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    File.WriteAllText("c:\\!del\\templog.txt", ex.Message);
            //}
            #endregion

            try
            {
                using (var conn = Connection())
                {
                    conn.Open();
                    SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, Source, Destination, Mask FROM Path";
                    SqlDataReader myReader = cmd.ExecuteReader();
                    while (myReader.Read())
                    {
                        string[] files = Directory.GetFiles(myReader["Source"].ToString(), myReader["Mask"].ToString());
                        foreach (string file in files)
                        {
                                string destFileName = Path.Combine(myReader["Destination"].ToString(), Path.GetFileName(file));
                                File.Copy(file, destFileName, true);
                                //SqlCommand insertCmd = new SqlCommand("INSERT INTO Files (Date, Path) values (@Date, @Path);", conn);
                                //cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = destFileName;
                                //var result = cmd.ExecuteNonQuery();
                        }
                    }
                    //File.WriteAllText("c:\\!del\\templog.txt", _result);
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("c:\\!del\\templog.txt", ex.Message);
            }

        }
    }
}
