﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Timers;


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
            //aTimer.Interval = Convert.ToDouble(GetSettingValue("Interval"))*3600000;//*час
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
                return null;
            }
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                using (var conn = Connection())
                {
                    conn.Open();
                    SqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, Source, Destination, Mask FROM Path";
                    SqlDataReader pathReader = cmd.ExecuteReader();
                    while (pathReader.Read())
                    {// Поиск каталогов в источнике и копирование их на ресурс назначения
                        string[] dirs = Directory.GetDirectories(pathReader["Source"].ToString(), pathReader["Mask"].ToString());
                        foreach (string sourceSubDir in dirs)
                        {
                            using (var checkConn = Connection())
                            {
                                checkConn.Open();
                                SqlCommand checkCmd = checkConn.CreateCommand();
                                checkCmd.CommandText = "SELECT Id FROM Objects WHERE Path=@Path";
                                string dest = pathReader["Destination"].ToString();
                                string pathObject = sourceSubDir.Replace(pathReader["Source"].ToString(), dest);
                                checkCmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = pathObject;
                                SqlDataReader checkReader = checkCmd.ExecuteReader();
                                if (!checkReader.HasRows)
                                { //Если не было ранее скопировано
                                    CopyDir(pathReader["Source"].ToString(), dest);
                                    //заносится информация об этом в БД
                                    checkReader.Close();
                                    SqlCommand insertCmd = new SqlCommand("INSERT INTO Objects (DateCreate, Path) values (GETDATE(), @Path);", checkConn);
                                    insertCmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = pathObject;
                                    var result = insertCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                    pathReader.Close();
                    cmd.CommandText = "SELECT Id, DateCreate, Path FROM Objects WHERE DateDelete IS NULL AND DateCreate<DATEADD(second,@DayCount*-1,GETDATE())";
                    //cmd.CommandText = "SELECT Id, DateCreate, Path FROM Objects WHERE DateDelete IS NULL DateCreate<DATEADD(day,@DayCount*-1,GETDATE())";
                    cmd.Parameters.Add("@DayCount", SqlDbType.Int).Value = Convert.ToInt32(GetSettingValue("StoragePeriodInDays"));
                    SqlDataReader ObjectsReader = cmd.ExecuteReader();
                    while (ObjectsReader.Read())
                    {
                        //Удаление файлов с истекшим сроком хранения
                        Directory.Delete(ObjectsReader["Path"].ToString(),true);
                        using (var updateConn = Connection())
                        {
                            updateConn.Open();
                            SqlCommand updateCmd = updateConn.CreateCommand();
                            updateCmd.CommandText = "UPDATE Objects SET DateDelete=GETDATE() WHERE Id=@Id;";
                            updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = ObjectsReader["Id"].ToString();
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string logfile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lastError.log");
                File.WriteAllText(logfile, ex.Message);
            }
        }

        private static void CopyDir(string sourceDir, string dest)
        {
            //Создать идентичное дерево каталогов
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourceDir, dest));

            //Скопировать все файлы. И перезаписать(если такие существуют)
            foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourceDir, dest), true);
        }
    }
}
