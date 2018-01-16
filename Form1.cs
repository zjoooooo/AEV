using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AEV
{
    public partial class Form1 : Form
    {
        string entrytimeofmc = null;
        string shiftStartTime = null;
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Init();
        }
        private void Init()
        {
            string constr = "Data Source=172.16.1.89;uid=secure;pwd=weishenme;database=AEV";
            string CommandText = "select CP,IP from ScanInterval order by Seq";
            DataSet ds = null;
            try
            {
                ds = SqlHelper.ExecuteDataset(constr, CommandType.Text, CommandText);
                LogClass.WriteLog("Loading Init Page Ok.");
            }
            catch (SqlException)
            {
                LogClass.WriteLog("Fail To Get Car Park List!");
            }
            DataRow row = ds.Tables[0].NewRow();
            row["CP"] = "All";
            ds.Tables[0].Rows.InsertAt(row, 0);
            comboBox1.DataSource = ds.Tables[0];
            comboBox1.DisplayMember = "CP";
            comboBox1.ValueMember = "IP";
            comboBox2.SelectedIndex = 0;
        }
        public void MainMethod(string startdate)
        {
            for (int i = 1; i <= 3; i++)
            {
                int scanningNumber = 0;
                string ShiftTime = null;
                if (i == 1)
                {
                    scanningNumber = 30;
                    ShiftTime = "07:18";
                }
                else if (i == 2)
                {
                    scanningNumber = 40;
                    ShiftTime = "15:15";
                }
                else if (i == 3)
                {
                    scanningNumber = 105;
                    ShiftTime = "23:12";
                }
                string constr = "Data Source=172.16.1.89;uid=secure;pwd=weishenme;database=AEV";
                string CommandText = "select CP,Interval,IP from ScanInterval order by Seq";
                SqlDataReader reader = null;
                try
                {
                    reader = SqlHelper.ExecuteReader(constr, CommandType.Text, CommandText);
                    LogClass.WriteLog("Got Car Park List!");
                }
                catch (SqlException)
                {
                    LogClass.WriteLog("Fail To Get Car Park List!");
                    continue;
                }
                //Test upload
                //Randomly choose shift start time by 10 mins buffer.
                shiftStartTime = Tools.ShiftStartTime(startdate, ShiftTime);
                LogClass.WriteLog($"Shift {i} starttime = {shiftStartTime}");
                while ((reader != null) & (reader.Read()))
                {
                    string carpark = reader["CP"].ToString();
                    int interval = Convert.ToInt16(reader["Interval"]);
                    string IP = reader["IP"].ToString();
                    LogClass.WriteLog("========================================================================");
                    LogClass.WriteLog($"Start At {carpark}");
                    //Shift start time + Travel time = enter car park time. 
                    DateTime MAXexitime = Convert.ToDateTime(entrytimeofmc).AddSeconds(570);    // Max ExitTime
                    if (carpark.Equals("U15"))  // First car park of batch.
                    {
                        entrytimeofmc = Tools.AddInterval(shiftStartTime, interval);
                    }
                    else
                    {
                        entrytimeofmc = Tools.AddInterval(MAXexitime.ToString("yyyy-MM-dd HH:mm:ss"), interval);
                    }
                    LogClass.WriteLog($"Motorcycle reach {carpark} entrytimeofmc={entrytimeofmc}");
                    InsertTransaction(carpark, IP, entrytimeofmc, scanningNumber, i);
                }
            }
            //Geng gai 
        }
        private void InsertTransaction(string carpark, string iP, string entrytimeofmc, int scanningNumber, int shift)
        {
            //  throw new NotImplementedException();
            //Get Entry and Exit Station ID.
            string constrCP = "Data Source=" + iP + ";uid=sa;pwd=yzhh2007;database=" + carpark;
            string cmd = @"select station_id,station_name from station_setup where station_type=1;
                           select station_id,station_name from station_setup where station_type=2;";
            LogClass.WriteLog($"IP={iP},Car Park={carpark}");
            DataSet ds = null;
            LogClass.WriteLog($"Reading Station ID.");
            try
            {
                ds = SqlHelper.ExecuteDataset(constrCP, CommandType.Text, cmd);
                LogClass.WriteLog("Got Station Id");
            }
            catch (SqlException e)
            {
                LogClass.WriteLog($"Error To Get Station Id {e.ToString()}");
                return;
            }
            //Get Entry and Exit ID List.
            List<string> EntryIDArray = new List<string>();
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                EntryIDArray.Add(dr["station_id"].ToString());
            }

            List<string> ExitIDArray = new List<string>();
            foreach (DataRow dr in ds.Tables[1].Rows)
            {
                ExitIDArray.Add(dr["station_id"].ToString());
            }
            string entryID = EntryIDArray[new Random().Next(0, EntryIDArray.Count - 1)];
            string exitID = ExitIDArray[new Random().Next(0, ExitIDArray.Count - 1)];
            LogClass.WriteLog($"entryID={entryID},exitID={exitID}");
            //After enter car park 7 mins to 9.5 mins exit.
            DateTime exitime = Convert.ToDateTime(entrytimeofmc).AddSeconds(new Random().Next(420, 570));
            //DB add time, already delay 1 second.
            DateTime exitadddate = exitime.AddSeconds(1);
            DateTime entryadddate = (Convert.ToDateTime(entrytimeofmc)).AddSeconds(1);
            TimeSpan ts = exitime - Convert.ToDateTime(entrytimeofmc);
            string parkedtime = (Convert.ToInt16(ts.TotalMinutes)).ToString();

            string CommandText = @"INSERT INTO entry_trans(station_id,entry_time,IU_Tk_no,trans_type,status,Tk_SerialNo,add_dt,paid_amt,parking_fee,gst_amt,card_type,Owe_Amt)VALUES(@entryid,@entrytime,@iu,7,0,0,@entryadddate,.00,.00,.00,0,.00);
                                   INSERT INTO exit_trans(station_id,exit_time,iu_tk_no,trans_type,parked_time,parking_fee,paid_amt,receipt_no,add_dt,status,redeem_time,gst_amt,card_type,top_up_amt)VALUES(@exitid,@exittime,@iu,7,@parkedtime,.00,.00,0,@exitadddate,0,0,.00,0,.00);
                                   INSERT INTO movement_trans(entry_station,entry_time,iu_tk_no,trans_type,exit_station,exit_time,parked_time,parking_fee,paid_amt,receipt_no,add_dt,update_dt,redeem_amt,redeem_time,card_type,top_up_amt,Owe_Amt)
                                                       VALUES(@entryid,@entrytime,@iu,7,@exitid,@exittime,@parkedtime,.00,.00,0,@entryadddate,@exitadddate,.00,0,0,.00,.00);";

            SqlParameter[] para = new SqlParameter[]
            {
                new SqlParameter("@entryid",entryID),
                new SqlParameter("@exitid",exitID),
                new SqlParameter("@entrytime",entrytimeofmc),
                new SqlParameter("@exittime",exitime.ToString("yyyy-MM-dd HH:mm:ss")),
                new SqlParameter("@entryadddate",entryadddate.ToString("yyyy-MM-dd HH:mm:ss")),
                new SqlParameter("@exitadddate",exitadddate.ToString("yyyy-MM-dd HH:mm:ss")),
                new SqlParameter("@iu","0714136597"),
                new SqlParameter("@parkedtime",parkedtime)
            };

            LogClass.WriteLog($"EntryID={entryID},ExitID={exitID},EntryTime={entrytimeofmc},ExitTime={exitime.ToString("yyyy-MM-dd HH:mm:ss")},PakredTime={parkedtime}");

            try
            {
                SqlHelper.ExecuteNonQuery(constrCP, CommandType.Text, CommandText, para);
                LogClass.WriteLog($"Success Insert Transaction Log To Entry ,Exit & Movementtransaction Table.");
            }
            catch (SqlException e)
            {
                LogClass.WriteLog($"Fail To Insert Transaction {e.ToString()}");
            }

            //Grab Data From PMS.
            //Get random number for scanning.
            int min = scanningNumber - 5;
            int max = scanningNumber + 5;
            int FinalNumber = new Random().Next(min, max);
            string Command = $"SELECT top {FinalNumber.ToString()} iu_tk_no,vehicle_no FROM [dbo].[movement_trans],season_mst where season_mst.season_no=movement_trans.iu_tk_no and movement_trans.entry_time<@entrytime and movement_trans.exit_time>@exittime and movement_trans.trans_type=2";
            SqlParameter[] grabPara = new SqlParameter[]
            {
                new SqlParameter("entrytime",entrytimeofmc),
                new SqlParameter("exittime",exitime.ToString("yyyy-MM-dd HH:mm:ss"))
            };
            SqlDataReader reader = null;
            try
            {
                reader = SqlHelper.ExecuteReader(constrCP, CommandType.Text, Command, grabPara);
                LogClass.WriteLog($"Get Season Vehcile In Car Park Between {entrytimeofmc} and {exitime.ToString("yyyy-MM-dd HH:mm:ss")}");
            }
            catch (SqlException e)
            {
                LogClass.WriteLog($"Fail To Get Season Vehcile In Car Park Between {entrytimeofmc} and {exitime.ToString("yyyy-MM-dd HH:mm:ss")}  {e.ToString()}");
                return;
            }
            string constr_AVE = $"Data Source=192.168.1.19;uid=sa;pwd=yzhh2007;database=ACISPCMS";
            string cmd_AVE = @"INSERT INTO [dbo].[Lot_Result](IUNo,LPN,Status1,DateTime1,Carpark,Location,UserID,Shift,Verified,Enforcement_IU)
                                                       VALUES(@IU,@licensePlate,'S',@recordtime,@carpark,'All','1111',@shift,1,'0714136597');";

            string startscanningTime = ((Convert.ToDateTime(entrytimeofmc)).AddSeconds(new Random().Next(60, 120))).ToString("yyyy-MM-dd HH:mm:ss");
            while ((reader != null) & (reader.Read()))
            {

                SqlParameter[] InsertPara = new SqlParameter[]
                {
                    new SqlParameter("@IU",reader["iu_tk_no"].ToString()),
                    new SqlParameter("@licensePlate",reader["vehicle_no"].ToString()),
                    new SqlParameter("@carpark",carpark),
                    new SqlParameter("@shift",shift.ToString()),
                    new SqlParameter("@recordtime",startscanningTime)
                };

                try
                {
                    SqlHelper.ExecuteNonQuery(constr_AVE, CommandType.Text, cmd_AVE, InsertPara);
                    LogClass.WriteLog($"Success Insert Transaction To AVE Server For {reader["iu_tk_no"].ToString()},{reader["vehicle_no"].ToString()},{carpark},{startscanningTime},Shift {shift.ToString()}");
                }
                catch (SqlException e)
                {
                    LogClass.WriteLog($"Fail To Insert Transaction For {reader["iu_tk_no"].ToString()},{reader["vehicle_no"].ToString()},{carpark},{startscanningTime},Shift {shift.ToString()},{e.ToString()}");
                    continue;
                }

                startscanningTime = ((Convert.ToDateTime(startscanningTime)).AddSeconds(new Random().Next(1, 5))).ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        public static void CleanData(DateTime dt, string cp,string shift)
        {
            string starttime=null, endtime=null;

            if (shift != null)
            {
                if (shift.Equals("1"))
                {
                    starttime = dt.ToString("yyyy-MM-dd 07:00:00");
                    endtime = dt.ToString("yyyy-MM-dd 11:00:00");
                }
                else if (shift.Equals("2"))
                {
                    starttime = dt.ToString("yyyy-MM-dd 15:00:00");
                    endtime = dt.ToString("yyyy-MM-dd 23:00:00");
                }
                else if (shift.Equals("3"))
                {
                    starttime = dt.ToString("yyyy-MM-dd 23:00:00");
                    endtime = dt.AddDays(1).ToString("yyyy-MM-dd 07:00:00");
                }
            }
            else
            {
                starttime = dt.ToString("yyyy-MM-dd 07:00:00");
                endtime = dt.AddDays(1).ToString("yyyy-MM-dd 07:00:00");
            }

            string constr = "Data Source=172.16.1.89;uid=secure;pwd=weishenme;database=AEV";
            string CommandText = null;
            if (cp == null)
            {
                CommandText = "select CP,Interval,IP from ScanInterval order by Seq";
            }
            else
            {
                CommandText = $"select CP,Interval,IP from ScanInterval WHERE CP='{cp}'";
            }

            SqlDataReader reader = null;
            try
            {
                reader = SqlHelper.ExecuteReader(constr, CommandType.Text, CommandText);
                LogClass.WriteLog("Got Car Park List!");
            }
            catch (SqlException e)
            {
                LogClass.WriteLog($"Fail To Get Car Park List!{e.ToString()}");
            }
            while ((reader != null) & (reader.Read()))
            {
                string carpark = reader["CP"].ToString();
                string IP = reader["IP"].ToString();
                LogClass.WriteLog("========================================================================");
                LogClass.WriteLog($"Start At {carpark}");
                string constrsp = "Data Source=" + IP + ";uid=sa;pwd=yzhh2007;database=" + carpark;
                string cmd = @"DELETE FROM [dbo].[movement_trans] where iu_tk_no='0714136597' and update_dt BETWEEN @starttime and @endtime;
                               DELETE FROM [dbo].[entry_trans] where iu_tk_no='0714136597' and add_dt BETWEEN @starttime and @endtime;
                               DELETE FROM [dbo].[exit_trans] where iu_tk_no='0714136597' and add_dt BETWEEN @starttime and @endtime;";


                SqlParameter[] para = new SqlParameter[]
                  {
                    new SqlParameter("@starttime",starttime),
                    new SqlParameter("@endtime",endtime)
                  };

                try
                {
                    SqlHelper.ExecuteNonQuery(constrsp, CommandType.Text, cmd, para);
                    LogClass.WriteLog("Clean Car Park Data Ok!");
                }
                catch (SqlException e)
                {
                    LogClass.WriteLog($"Fail To Clean Car Park Data!{e.ToString()}");
                }
            }
        }
        private void Multi_Date()
        {
            List<string> li = new List<string>();
            DateTime dt = Convert.ToDateTime("2017-11-01");           
            for(int i = 0; i <= 30; i++)
            {
                li.Add(dt.AddDays(i).ToString("yyyy-MM-dd "));
            }
            
            foreach(string str in li)
            {
                LogClass.WriteLog($"=========={str}==========");
                MainMethod(str);
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            string Cp = comboBox1.Text.ToString();
            string Ip = comboBox1.SelectedValue.ToString();
           
            if (Cp.Equals("All"))
            {
                string startdate = dateTimePicker1.Value.ToString("yyyy-MM-dd ");
                //   Thread thr = new Thread(() => MainMethod(startdate));
                Thread thr = new Thread(() => Multi_Date());
                thr.Start();
            }
            else
            {
                string entrytimeofmc = dateTimePicker2.Value.ToString("yyyy-MM-dd HH:mm:ss");
                int shift = Convert.ToInt16(comboBox2.Text.ToString());                
                int scanningNumber = 0;
                if (shift == 1)
                {
                    scanningNumber = 30;
                }
                else if (shift == 2)
                {
                    scanningNumber = 40;
                }
                else if (shift == 3)
                {
                    scanningNumber = 55;
                }
                Thread thr = new Thread(() => InsertTransaction(Cp, Ip, entrytimeofmc, scanningNumber, shift));
                thr.Start();
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            string Cp = comboBox1.Text.ToString();
            DateTime dt = dateTimePicker1.Value;
            string shift = comboBox2.Text.ToString();
            if (Cp.Equals("All"))
            {
                Thread thr = new Thread(() => CleanData(dt, cp: null,shift:null));
                thr.Start();
            }
            else
            {
                if (shift.Equals("All"))
                {
                    Thread thr = new Thread(() => CleanData(dt, Cp,shift:null));
                    thr.Start();
                }
                else
                {
                    Thread thr = new Thread(() => CleanData(dt, Cp,shift));
                    thr.Start();
                }

            }

        }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
