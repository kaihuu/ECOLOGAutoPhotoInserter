using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Windows.Forms;
using System.Collections;
using System.ComponentModel;
using System.Collections.Generic;

namespace ECOLOG_AutoPhotoInserter
{
    public partial class MainForm : Form
    {
        string[] folders;
        string UpdatePath = "";
        string LogPath = "";
        System.Text.Encoding enc = System.Text.Encoding.GetEncoding("shift_jis");
        string machine = Environment.MachineName;
        string monitorPath = "";
        string monitored = "";

        public MainForm()
        {
            InitializeComponent();

            UpdatePath = @"./LastUpdate.txt";
            LogPath = @"./Log.txt";
            monitorPath = @"./monitorPath.txt";

            if (System.IO.File.Exists(monitorPath))
            {
                if (machine == "ITSSERVER")
                {
                    monitored = System.IO.File.ReadAllText(monitorPath, enc);
                }
                else
                {
                    monitored = @"\\itsserver\ECOLOG_Photo_itsserver\ECOLOG_Photo";
                }

                if (System.IO.Directory.Exists(monitored))
                {
                    label3.Text = monitored;
                }
                else
                {
                    SaveLog("監視対象が見つかりません");
                    label3.Text = "監視対象が見つかりません";
                    button1.Enabled = false;
                }
            }
            else
            {
                SaveLog("設定ファイルが見つかりません");
                label3.Text = "設定ファイルが見つかりません";
                button1.Enabled = false;
                
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

            Console.WriteLine(" Thread start ... ");

            try
            {
                if (button1.Text == "監視開始")
                {
                    BeginInsert();
                    timer1.Interval = 1000 * 60 * 60;
                    timer1.Enabled = true;
                }
                else if (button1.Text == "監視停止")
                {
                    timer1.Enabled = false;
                    button1.Text = "監視開始";
                    label1.Text = "停止中";
                }
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                SaveLog("ディレクトリが見つかりません");
                backgroundWorker1.CancelAsync();
                timer1.Enabled = false;
                button1.Text = "エラーがありました";
                label1.Text = "停止中";
            }
            catch (Exception ex)
            {
                SaveLog(ex.ToString());
                backgroundWorker1.CancelAsync();
                timer1.Enabled = false;
                button1.Text = "エラーがありました";
                label1.Text = "停止中";
            }
        }

        private void BeginInsert()
        {
            try
            {
                if (!backgroundWorker1.IsBusy)
                {
                    progressBar1.Minimum = 0;
                    progressBar1.Maximum = 10;
                    progressBar1.Value = 0;
                    folders = GetNotInsertedFolders(monitored);

                    SaveLog("CheckFolders");
                    int num = 0;
                    if (folders.Length > 0)
                    {
                        foreach (string folder in folders)
                        {
                            num += Directory.GetFiles(folder, "*.jpg", SearchOption.TopDirectoryOnly).Length;
                        }
                        progressBar1.Maximum = num+1;
                        progressBar1.Visible = true;
                        label2.Text = num.ToString() + "件";
                        label2.Visible = true;

                        backgroundWorker1.RunWorkerAsync();
                        button1.Text = "監視停止";
                        label1.Text = "インサート中";
                        SaveLog("InsertStart");
                    }
                    else
                    {
                        button1.Text = "監視停止";
                        label1.Text = "監視中";
                    }
                }
                else
                {

                }
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                SaveLog("ディレクトリが見つかりません");
                backgroundWorker1.CancelAsync();
                timer1.Enabled = false;
                button1.Text = "エラーがありました";
                label1.Text = "停止中";
            }
            catch (Exception ex)
            {
                SaveLog(ex.ToString());
                backgroundWorker1.CancelAsync();
                timer1.Enabled = false;
                button1.Text = "エラーがありました";
                label1.Text = "停止中";
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            sqlConnection1.Open();
            try
            {
                string carDir;
                int carID;
                int driverID;

                int counter = 0;
                foreach (string folder in folders)
                {
                    string[] files = Directory.GetFiles(folder, "*.jpg", SearchOption.TopDirectoryOnly);
                    string[] times = getDatetime(folder, files);
                    int numOfFiles = files.Length;

                    carDir = Directory.GetParent(folder).ToString();
                    carID = int.Parse(Path.GetFileName(carDir).Substring(0, 2));
                    driverID = int.Parse(Path.GetFileName(Directory.GetParent(carDir).ToString()).Substring(0, 2));

                    int i = 0;
                    while (i < numOfFiles)
                    {
                        SqlCommand cmd = new SqlCommand();
                        cmd.Connection = sqlConnection1;
                        if (times[i] != null)
                        {
                            cmd.CommandText = @"SELECT COUNT(*) FROM CORRECTED_PICTURE 
                                                        WHERE DRIVER_ID = @DRIVER_ID  
                                                        and JST = @JST ";
                            cmd.Parameters.Add("@DRIVER_ID", SqlDbType.Int).Value = driverID;
                            cmd.Parameters.Add("@JST", SqlDbType.DateTime).Value = times[i];

                            int check = (int)cmd.ExecuteScalar();

                            if (check == 0)
                            {
                                cmd.CommandText = @"INSERT INTO CORRECTED_PICTURE (DRIVER_ID, CAR_ID, SENSOR_ID, JST, PICTURE) 
                                                                       VALUES (@DRIVER_ID, @CAR_ID, @SENSOR_ID, @JST, @PICTURE)";

                                //Read jpg into file stream, and from there into Byte array.
                                FileStream fsBLOBFile = new FileStream(files[i], FileMode.Open, FileAccess.Read);
                                Byte[] bytBLOBData = new Byte[fsBLOBFile.Length];
                                fsBLOBFile.Read(bytBLOBData, 0, bytBLOBData.Length);
                                fsBLOBFile.Close();

                                cmd.Parameters.Add("@CAR_ID", SqlDbType.Int).Value = carID;
                                cmd.Parameters.Add("@PICTURE", SqlDbType.VarBinary).Value = bytBLOBData;
                                cmd.Parameters.Add("@SENSOR_ID", SqlDbType.Int).Value = 19;

                                cmd.ExecuteNonQuery();
                            }
                        }
                        i++;
                        counter++;
                        backgroundWorker1.ReportProgress(counter);
                    }
                }
                SaveLog(counter + "件挿入");
            }
            finally
            {
                sqlConnection1.Close();
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                SaveLog("InsertError\r\n" + e.Error.Message);
                
            }
            else
            {
                SaveLog("InsertComplete");
                System.IO.File.WriteAllText(UpdatePath, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), enc);
            }
            label1.Text = "監視中";
            progressBar1.Visible = false;
            label2.Visible = false;
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private string[] GetNotInsertedFolders(string folderPass)
        {
            try
            {
                string str = System.IO.File.ReadAllText(UpdatePath, enc);
                DateTime LastUpdate = DateTime.Parse(str);
                DirectoryInfo di = new DirectoryInfo(folderPass);
                DirectoryInfo[] diArr = di.GetDirectories("201?????-??????", SearchOption.AllDirectories);
                ArrayList NotInsertedFolders = new ArrayList();

                for(int i = 0; i < diArr.Length; i++)
                {
                    try
                    {
                        if (diArr[i].Parent.Name != "AirConditioner" && diArr[i].CreationTime > LastUpdate)
                        {
                            NotInsertedFolders.Add(diArr[i].FullName);
                        }
                    }
                    catch (System.UnauthorizedAccessException)//隠しファイルなどのアクセス許可のないファイルをキャッチ(何もしない)
                    {

                    }
                }

                string[] FolderList;
                if (NotInsertedFolders.Count > 0)
                {
                    FolderList = (string[])NotInsertedFolders.ToArray(typeof(string));
                }
                else
                {
                    FolderList = new string[0];
                }
                return FolderList;
            }
            finally
            {

            }
        }

        private string[] getDatetime(string folder, string[] files)
        {
            int numOfFiles = files.Length;
            int counter = 0;
            string[] sFiles = new string[numOfFiles];
            DateTime[] dtOfFiles = new DateTime[numOfFiles];
            string[] sDT = new string[numOfFiles];
            ArrayList list = new ArrayList();
            string milliSec;

            int lag = getLag(folder);

            while (counter < numOfFiles)
            {
                sFiles[counter] = Path.GetFileNameWithoutExtension(files[counter]);

                dtOfFiles[counter] = DateTime.ParseExact(sFiles[counter], "yyyyMMdd-HHmmss_fff",
                            System.Globalization.DateTimeFormatInfo.InvariantInfo, System.Globalization.DateTimeStyles.None);
                dtOfFiles[counter] = dtOfFiles[counter].AddSeconds(lag);

                milliSec = dtOfFiles[counter].ToString("fff");
                if (int.Parse(milliSec) > 499)
                    dtOfFiles[counter].AddSeconds(1);
                milliSec = "000";

                if (list.Contains(dtOfFiles[counter].ToString("yyyy-MM-dd HH:mm:ss") + "." + milliSec))
                    sDT[counter] = null;
                else
                {
                    sDT[counter] = dtOfFiles[counter].ToString("yyyy-MM-dd HH:mm:ss") + "." + milliSec;
                    list.Add(sDT[counter]);
                }
                counter++;
            }
            return sDT;
        }
        

        private int getLag(string folder)
        {
            string TimeLagPath = @"\\133.34.154.116\ECOLOG_Photo_itsserver\ECOLOG_Photo\TimeLag.txt";
            string[] lines = System.IO.File.ReadAllLines(TimeLagPath, enc);
            string[][] table = new string[lines.Length][];
            for (int i = 0; i < lines.Length; i++)
            {
                table[i] = lines[i].Split(',');
            }

            //フォルダの作成日時を取得
            DateTime dt = DateTime.ParseExact(Path.GetFileName(folder), "yyyyMMdd-HHmmss",
                System.Globalization.DateTimeFormatInfo.InvariantInfo, System.Globalization.DateTimeStyles.None);

            //tableの中で作成日時に一番近い行を取得
            int min = 1000000000;
            int index = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                TimeSpan ts = dt - DateTime.ParseExact(table[i][1], "yyyy/MM/dd HH:mm:ss",
                    System.Globalization.DateTimeFormatInfo.InvariantInfo, System.Globalization.DateTimeStyles.None);
                int second = Math.Abs((int)ts.TotalSeconds);
                if (second < min)
                {
                    min = second;
                    index = i;
                }
            }

            if (index > -1)
            {
                return int.Parse(table[index][2]);
            }
            else
                return 0;
        }

        public void SaveLog(string Log)
        {
            
            File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " :" + Log + "\r\n", enc);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            BeginInsert();
        }

    }
}
