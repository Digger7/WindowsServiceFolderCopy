using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Configurator
{
    public partial class Form1 : Form
    {
        private static SqlConnection Connection()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database.mdf"); ;
            return new SqlConnection($"Data Source = (LocalDB)\\MSSQLLocalDB; AttachDbFilename = {path}; Integrated Security = True");
        }

        public Form1()
        {
            InitializeComponent();

            numericUpDownInterval.Value = Convert.ToDecimal(GetSettingValue("Interval"));
            numericUpDownPeriod.Value = Convert.ToDecimal(GetSettingValue("StoragePeriodInDays"));
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

        private void Form1_Load(object sender, EventArgs e)
        {
            // TODO: данная строка кода позволяет загрузить данные в таблицу "databaseDataSet.Path". При необходимости она может быть перемещена или удалена.
            this.pathTableAdapter.Fill(this.databaseDataSet.Path);

        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show("Сохранить изменения и перезапустить службу?","Подтвердите",MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirmResult == DialogResult.Yes)
            {
                this.pathTableAdapter.Update(databaseDataSet);
                using (var updateConn = Connection())
                {
                    updateConn.Open();
                    SqlCommand updateCmd = updateConn.CreateCommand();
                    updateCmd.CommandText = "UPDATE Settings SET Value=@Value WHERE Name=@Name;";
                    updateCmd.Parameters.Add("@Name", SqlDbType.NVarChar).Value = "Interval";
                    updateCmd.Parameters.Add("@Value", SqlDbType.NVarChar).Value = numericUpDownInterval.Value.ToString();
                    updateCmd.ExecuteNonQuery();

                    updateCmd.Parameters["@Name"].Value = "StoragePeriodInDays";
                    updateCmd.Parameters["@Value"].Value = numericUpDownPeriod.Value.ToString();
                    updateCmd.ExecuteNonQuery();

                    RestartService("GuardService");
                }
            }
        }

        private void RestartService(string serviceName)
        {
            System.Diagnostics.Process.Start("net", $"stop {serviceName}");
            System.Diagnostics.Process.Start("net", $"start {serviceName}");
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show("Очистить таблицу скопированных каталогов?", "Подтвердите", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirmResult == DialogResult.Yes)
            {
                using (var truncateConn = Connection())
                {
                    truncateConn.Open();
                    SqlCommand truncateCmd = truncateConn.CreateCommand();
                    truncateCmd.CommandText = "TRUNCATE TABLE Objects;";
                    truncateCmd.ExecuteNonQuery();
                }
            }
        }
    }
}
