using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AEV
{
    class Tools
    {
        //增加随机10分钟的时间
        public static string ShiftStartTime(string startDate,string dtstr)
        {
            DateTime DT = Convert.ToDateTime(startDate + dtstr);
            Random r = new Random();
            DateTime RealStart = DT.AddSeconds(r.Next(0, 600));
            string time = RealStart.ToString("yyyy-MM-dd HH:mm:ss");
            LogClass.WriteLog(time);
            return time;
        }
        //增加Inverval的时间, 单位是min.
        public static string AddInterval(string startTime,int interval)
        {

            DateTime DT = Convert.ToDateTime(startTime);
            int seconds = interval * 60;
            return DT.AddSeconds(new Random().Next(seconds-10, seconds+10)).ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
