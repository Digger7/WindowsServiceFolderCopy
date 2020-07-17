using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
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
        private static bool noCopyOldFolder = GetSettingValue("NoCopyOldFolder")=="1"?true:false;
        private static int storagePeriodInDays = Convert.ToInt32(GetSettingValue("StoragePeriodInDays"));
        private static System.Timers.Timer aTimer;
        private static double interval = Convert.ToDouble(GetSettingValue("Interval")) * 3600000;//*час;
        //private static double interval = Convert.ToDouble(GetSettingValue("Interval")) * 60000;//Для отладки;

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
            aTimer = new System.Timers.Timer();

            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
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
                    cmd.CommandText = "SELECT Id, Source, Destination FROM Path";
                    SqlDataReader pathReader = cmd.ExecuteReader();
                    while (pathReader.Read())
                    {// Поиск каталогов в источнике и копирование их на ресурс назначения
                        string[] dirs = Directory.GetDirectories(pathReader["Source"].ToString());
                        foreach (string sourceSubDir in dirs)
                        {
                            using (var checkConn = Connection())
                            {
                                long sourceSubDirSize = Directory.EnumerateFiles(sourceSubDir, "*", SearchOption.AllDirectories).Sum(fileInfo => new FileInfo(fileInfo).Length);

                                checkConn.Open();
                                SqlCommand checkCmd = checkConn.CreateCommand();
                                checkCmd.CommandText = "SELECT Id, Size FROM Objects WHERE Path=@Path";
                                string dest = pathReader["Destination"].ToString();
                                string pathObject = sourceSubDir.Replace(pathReader["Source"].ToString(), dest);
                                checkCmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = pathObject;
                                SqlDataReader checkReader = checkCmd.ExecuteReader();
                                checkReader.Read();

                                bool sizeChanged = false;
                                if(checkReader.HasRows) sizeChanged = sourceSubDirSize != Convert.ToInt64(checkReader["Size"]) ? true : false;

                                //if (!noCopyOldFolder || Directory.GetCreationTime(sourceSubDir) > DateTime.Now.AddSeconds(storagePeriodInDays * -1))// Для отладки
                                if(!noCopyOldFolder || Directory.GetCreationTime(sourceSubDir) > DateTime.Now.AddDays(storagePeriodInDays*-1))
                                    if (!checkReader.HasRows || sizeChanged)//Если записи нет или размер папки изменился
                                    { //Если не было ранее скопировано и размер папки не изменился
                                        if(!Directory.Exists(pathObject)) Directory.CreateDirectory(pathObject);
                                        CopyDir(sourceSubDir, pathObject);

                                        checkReader.Close();
                                        SqlCommand objectsCmd;
                                        if (!sizeChanged)
                                        {
                                            objectsCmd = new SqlCommand("INSERT INTO Objects (DateCreate, Path, Size) values (GETDATE(), @Path, @Size);", checkConn);
                                        }
                                        else {
                                            objectsCmd = new SqlCommand("UPDATE Objects SET Size=@Size WHERE Path=@Path;", checkConn);
                                        }
                                        objectsCmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = pathObject;
                                        objectsCmd.Parameters.Add("@Size", SqlDbType.Int).Value = sourceSubDirSize;
                                        var result = objectsCmd.ExecuteNonQuery();
                                    }
                            }
                        }
                    }
                    pathReader.Close();
                    cmd.CommandText = "SELECT Id, DateCreate, Path FROM Objects WHERE DateDelete IS NULL AND DateCreate<DATEADD(second,@DayCount*-1,GETDATE())"; // Для отладки
                    //cmd.CommandText = "SELECT Id, DateCreate, Path FROM Objects WHERE DateDelete IS NULL AND DateCreate<DATEADD(day,@DayCount*-1,GETDATE())";
                    cmd.Parameters.Add("@DayCount", SqlDbType.Int).Value = storagePeriodInDays;
                    SqlDataReader ObjectsReader = cmd.ExecuteReader();
                    while (ObjectsReader.Read())
                    {
                        //Удаление файлов с истекшим сроком хранения
                        Directory.Delete(ObjectsReader["Path"].ToString(),true);
                        using (var updateConn = Connection())
                        {
                            updateConn.Open();
                            SqlCommand updateCmd = updateConn.CreateCommand();
                            if (noCopyOldFolder)
                            {
                                updateCmd.CommandText = "DELETE FROM Objects WHERE Id=@Id;";
                            }
                            else {
                                updateCmd.CommandText = "UPDATE Objects SET DateDelete=GETDATE() WHERE Id=@Id;";
                            }
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

            aTimer.Stop();
            aTimer.Interval = interval;
            aTimer.Start();
        }

        private static void CopyDir(string sourceDir, string dest)
        {
            //Создать идентичное дерево каталогов
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourceDir, dest));

            //Скопировать все файлы. И перезаписать(если такие существуют)
            foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                //File.Copy(newPath, newPath.Replace(sourceDir, dest), true);
                string filePath = newPath.Replace(sourceDir, dest);
                if (!File.Exists(filePath))
                    File.Copy(newPath, filePath, true);
            }
        }
    }
}
