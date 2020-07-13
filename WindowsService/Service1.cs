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

        private static string GetSettingValue(string name)
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
                    SqlDataReader pathReader = cmd.ExecuteReader();
                    while (pathReader.Read())
                    {// Поиск файлов в каталогах источниках и копирование их на ресурс назначения
                        string[] files = Directory.GetFiles(pathReader["Source"].ToString(), pathReader["Mask"].ToString());
                        foreach (string file in files)
                        {
                            using (var checkConn = Connection()) {
                                checkConn.Open();
                                SqlCommand checkCmd = checkConn.CreateCommand();
                                checkCmd.CommandText = "SELECT Id FROM Files WHERE Path=@Path";
                                string destFileName = Path.Combine(pathReader["Destination"].ToString(), Path.GetFileName(file));
                                checkCmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = destFileName;
                                SqlDataReader checkReader = checkCmd.ExecuteReader();
                                if (!checkReader.HasRows)
                                { // Если файл не был ранее скопирован
                                    File.Copy(file, destFileName, true); // Выполняется копирование
                                    //И заносится информация об этом в БД
                                    checkReader.Close();
                                    SqlCommand insertCmd = new SqlCommand("INSERT INTO Files (Date, Path) values (GETDATE(), @Path);", checkConn);
                                    insertCmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = destFileName;
                                    var result = insertCmd.ExecuteNonQuery();
                                }
                            }

                        }
                    }
                    pathReader.Close();
                    //cmd.CommandText = "SELECT Id, Date, Path FROM Files WHERE Date<DATEADD(second,@DayCount*-1,GETDATE())";
                    cmd.CommandText = "SELECT Id, Date, Path FROM Files WHERE Date<DATEADD(day,@DayCount*-1,GETDATE())";
                    //cmd.Parameters.Add("@DayCount", SqlDbType.Int).Value = 90;
                    cmd.Parameters.Add("@DayCount", SqlDbType.Int).Value = Convert.ToInt32(GetSettingValue("StoragePerioInDays"));
                    SqlDataReader filesReader = cmd.ExecuteReader();
                    while (filesReader.Read())
                    {
                        //Удаление файлов с истекшим сроком хранения
                        File.Delete(filesReader["Path"].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                string logfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                File.WriteAllText(logfile, ex.Message);
            }

        }
    }
}
