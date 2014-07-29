using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using Reclamation.Core;
using System.Configuration;
using System.IO;
using System.Data.Linq;
using System.Linq;

namespace Reclamation.TimeSeries.Hydromet
{
    /// <summary>
    /// Reads Daily Data from Hydromet 'archives'
    /// </summary>
    public class HydrometDailySeries : Reclamation.TimeSeries.Series
    {
        private string pcode;
        private string cbtt;
        private HydrometHost server;


        public HydrometDailySeries(string cbtt, string pcode, HydrometHost server)
        {
            Init(cbtt, pcode, server);
        }

        //public HydrometDailySeries(string seriesName)
        //{
        //    var tokens = seriesName.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        //    if (tokens.Length != 2)
        //        throw new ArgumentException("Invalid series name " + seriesName);
        //    Init(tokens[0],tokens[1], HydrometHost.PN);
        //}

        public HydrometDailySeries(string cbtt, string pcode)
        {
            Init(cbtt, pcode, HydrometHost.PN);
        }

        public HydrometDailySeries(TimeSeriesDatabase db, TimeSeriesDatabaseDataSet.SeriesCatalogRow sr):base(db,sr)
        {
            HydrometInfoUtility.ParseConnectionString(ConnectionString, out server, out cbtt, out pcode);
            State = HydrometInfoUtility.LookupState(cbtt);
        }


        private void Init(string cbtt, string pcode, HydrometHost server)
        {
            this.server = server;
            this.cbtt = cbtt;
            State = HydrometInfoUtility.LookupState(cbtt);
            Parameter = pcode;
            this.pcode = pcode;
            this.Table.TableName = cbtt.Trim() + "_" + pcode.Trim(); ;
            Units = HydrometInfoUtility.LookupArchiveUnits(pcode);
            this.Name = HydrometInfoUtility.LookupSiteDescription(cbtt) + " " + Units;
            this.SiteName = HydrometInfoUtility.LookupSiteDescription(cbtt);
            this.TimeInterval = TimeSeries.TimeInterval.Daily;
            ReadOnly = true;
            Source = "Hydromet";
            Provider = "HydrometDailySeries";
            ConnectionString = "server=" + server
            + ";cbtt=" + cbtt + ";pcode=" + pcode+";LastUpdate=" + DateTime.Now.ToString(DateTimeFormatInstantaneous);
            Appearance.LegendText = Name;
        }

        protected override Series CreateFromConnectionString()
        {
            string str = ConnectionStringToken("server");
            HydrometHost svr = (HydrometHost)Enum.Parse(typeof(HydrometHost), str);

            HydrometDailySeries s = new HydrometDailySeries(
            ConnectionStringToken("cbtt"), ConnectionStringToken("pcode"),svr);

            return s;
        }
        protected override void ReadCore()
        {
            if (m_db != null)
                ReadCore(TimeSeriesDatabase.MinDateTime, TimeSeriesDatabase.MaxDateTime);
            else
            {
                var por = HydrometInfoUtility.ArchivePeriodOfRecord(cbtt, pcode);
                ReadCore(por.T1,por.T2);
            }
        }

         public static HydrometDailySeries Read(string cbtt, string pcode,
              DateTime t1, DateTime t2, HydrometHost server)
        {
            HydrometDailySeries s = new HydrometDailySeries(cbtt, pcode, server);
            s.ReadCore(t1, t2);
            return s;
        }


        

         protected override void ReadCore(DateTime t1, DateTime t2)
        {
            Clear();

            if (m_db == null || HydrometInfoUtility.WebOnly)
            {
                ReadFromHydromet(t1, t2);
            }
            else
            {
                if (HydrometInfoUtility.AutoUpdate)
                {
                    if (t2 >= DateTime.Now.Date.AddDays(1) && t2.Year <6189)
                    { // don't waste time looking to the future
                        t2 = DateTime.Now.AddDays(-1).Date;
                    }
                    base.UpdateCore(t1, t2, true);
                }
                base.ReadCore(t1, t2);
            }
        }


         public static HydrometDataCache Cache; // in-memory cache 

        private void ReadFromHydromet(DateTime t1, DateTime t2)
        {

            string key = cbtt + " " + pcode;
            int tableIndex = -1;
            if (Cache != null
                && (tableIndex = Cache.TableIndex(key, TimeSeries.TimeInterval.Daily, t1, t2)) >= 0)
            {
                int idx = Cache.DataSet.Tables[tableIndex].Columns.IndexOf(key);
                if (idx < 0)
                    throw new Exception("oops");
                //Console.WriteLine("Cache hit for: " + key);
                ReadFromTable(Cache.DataSet.Tables[tableIndex], idx, t1, t2);
            }
            else
            {
                ReadFromWeb(t1, t2);
            }
        }

        private void ReadFromTable(DataTable tbl, int dataIndex, DateTime t1, DateTime t2)
        {
            this.Clear();
            for (int i = 0; i < tbl.Rows.Count; i++)
            {
                DateTime t = Convert.ToDateTime(tbl.Rows[i][0]);

                if (t < t1 || t > t2)
                    continue;

                if (Convert.IsDBNull(tbl.Rows[i][dataIndex]))
                {
                    AddMissing(t);
                }
                else
                {
                    double d = Convert.ToDouble(tbl.Rows[i][dataIndex]);
                    Add(t, d);
                }
            }
        }



        private void ReadFromWeb(DateTime t1, DateTime t2)
        {
            if (t2 >= DateTime.Now && t2.Year < 6000 )
            {
                t2 = DateTime.Now.Date;
            }

            string payload = "parameter=" + cbtt + " " + pcode
                + "&syer=" + t1.Year.ToString()
                + "&smnth=" + t1.Month.ToString()
                + "&sdy=" + t1.Day.ToString()
                + "&eyer=" + t2.Year.ToString()
                + "&emnth=" + t2.Month.ToString()
                + "&edy=" + t2.Day.ToString()
                + "&format=2";

            string query = HydrometURL.GetUrlToDataCgi(server, TimeSeries.TimeInterval.Daily);

            string[] data = Web.GetPage(query, payload, HydrometInfoUtility.WebCaching);

            TextFile tf = new TextFile(data);

            int idx1 = tf.IndexOf("BEGIN DATA");

            int idx2 = tf.IndexOf("END DATA");

            if (idx1 < 0 || idx2 < 0 || idx2 == idx1 + 1)
            {
                Logger.WriteLine("no data found");
            }
            Messages.Add(query + "&" + payload);

            int errorCount = 0;
            for (int i = idx1 + 2; i < idx2; i++)
            {
                string[] tokens = data[i].Split(',');

                if (tokens.Length != 2)
                {
                    continue;
                }

                DateTime t;
                if (DateTime.TryParse(tokens[0], out t))
                {
                    double result = Point.MissingValueFlag;
                    if (!double.TryParse(tokens[1], out result))
                    {
                        if (errorCount < 50)
                            Logger.WriteLine("Error parsing " + data[i]);
                        errorCount++;
                        AddMissing(t);

                    }
                    else
                    {
                        Add(t, result);
                    }
                }
            }
            Messages.Add("Returned " + Count + " records ");
        }

        /// <summary>
        /// Offset approx 24 hours, for storage and reservoir water level
        /// since these are midnight readings.
        /// </summary>
        /// <returns></returns>
        private TimeSpan DetermineTimeSpan()
        {
            TimeSpan ts;
            if (pcode.ToLower().Trim() == "fb" || pcode.ToLower().Trim() == "af")
                ts = new TimeSpan(23, 59, 59);
            else
                ts = new TimeSpan(0, 0, 0);
            return ts;
        }

        /// <summary>
        /// Reads one year of Daily (Arcive) data
        /// used to support Hydromet FORTRAN calls to  GETACFREC() 
        /// </summary>
        public static void GetAcfRec(string cbtt, string pcode, string waterYear, double[,] values,
            int lagDays=0)
        {
            YearRange rng = new YearRange(int.Parse(waterYear), 10);
            HydrometDailySeries s = new HydrometDailySeries(cbtt, pcode);
            s.Read(rng.DateTime1.AddDays(-lagDays), rng.DateTime2.AddDays(-lagDays));

            for (int i = 0; i < s.Count; i++)
            {
                var pt = s[i];
                pt.DateTime = pt.DateTime.AddDays(lagDays);
                int m = HydrometMonth[pt.DateTime.Month];
                int d = pt.DateTime.Day;
                values[m,d] = s[i].Value;
            }

        }

        //                             0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12}
        static int[] HydrometMonth = { 0, 4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3 };


        ///// <summary>
        ///// Saves Series data to Archives. This will overwrite existing data
        ///// uses:
        /////    Series.SiteName as cbtt
        /////    Series.Parameter as pcode
        ///// </summary>
        ///// <param name="list"></param>
        ///// <param name="?"></param>
        //public static void SaveToArchives(SeriesList list, string username, string password)
        //{
        //    string hydrometScript = WriteToArcImportFile(list);

        //    string remoteFile = HydrometDataUtility.CreateRemoteFileName(username, HydrometDataBase.Archives);
            
        //    if (LinuxUtility.IsLinux())
        //    {
        //        // copy file to server using scp -- 


        //    }
        //    else
        //    {
        //        var rval = HydrometEdits.RunArcImport(username, password, hydrometScript, remoteFile, true, false, false);
        //        Logger.WriteLine(rval);
        //    }
        //}


        /// <summary>
        /// .A ARKI1 20121104 M DH2400/QRDRG 0.435
        /// .A LUCI1 20121026 M DH2400/QADRZ 0.802
        /// </summary>
        /// <param name="controlFileName"></param>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <returns></returns>
        public static string[] CreateSHEFA(string controlFileName, DateTime t1, DateTime t2)
        {
            var rval = new List<string>();
            var tmp = Cache;

            CsvFile csv = new CsvFile(controlFileName, 
                new string[] { "String", "String", "String", "String", "String", "Double" });

            SetupCacheForShef(csv,t1,t2);

            foreach (DataRow row in csv.Rows)
            {
                // read hydromet
                var s = new HydrometDailySeries(row["cbtt"].ToString(),
                                           row["pcode"].ToString());
                s.Read(t1, t2);
                s.RemoveMissing();

                foreach (var pt in s)
	{
                    if( pt.IsMissing)
                        continue;

                    double d = pt.Value * Convert.ToDouble(row["scale"]);

                    string A = ".A " + row["shef_locid"].ToString()
                          + " " + pt.DateTime.ToString("yyyyMMdd")
                          + " " + row["time_zone"].ToString()
                          + " DH2400/" + row["shef_tag"].ToString()
                          + " " + d.ToString("F3");


                rval.Add(A);
                }
            }


            Cache = tmp;
            return rval.ToArray();
        }

        private static void SetupCacheForShef(CsvFile csv,DateTime t1, DateTime t2)
        {
            
            Cache = new HydrometDataCache();

            // get list of pcodes and cbtts
            var query = from row in csv.AsEnumerable()
                        select row.Field<string>("cbtt")
                          +" "+  row.Field<string>("pcode");
                           
           Cache.Add(query.ToArray(),t1,t2, HydrometHost.PN,TimeInterval.Daily);

        }


        public static void WriteToArcImportFile(Series s, string cbtt, string pcode, string outputFileName,
            bool append=false)
        {
            SeriesList A = new SeriesList();
            s.SiteName = cbtt;
            s.Parameter = pcode;
            A.Add(s);
            WriteToArcImportFile(A,outputFileName,append);
        }
        /// <summary>
        /// Writes Daily Series data to a temporary file.
        /// hydromet cbtt is copied from Series[].SiteName
        /// hydromet pcode is copied from Series[].Parameter
        /// </summary>
        /// <param name="list"></param>
        /// <returns>FileName created with Series data</returns>
        public static void WriteToArcImportFile(SeriesList list, string outputFileName, bool append=false)
        {
            //string hydrometScript = FileUtility.GetTempFileName(".txt")
            bool oldFile = File.Exists(outputFileName);

            StreamWriter output = new StreamWriter(outputFileName,append);
            // first column is date, other columns are values
            if (!oldFile)
            {
                output.WriteLine("MM/dd/yyyy cbtt         PC        NewValue      OldValue ");
            }
           
               

                for (int i = 0; i < list.Count; i++)
                {
                    Series s = list[i];

                    if (s.TimeInterval != TimeInterval.Daily)
                    {
                        Logger.WriteLine("Warning: series that is not daily will not be saved");
                        continue;
                    }
                    for (int j = 0; j < s.Count; j++)
                    {
                        Point pt = s[j];
                        double val = pt.Value;
                        if (pt.IsMissing || double.IsNaN(pt.Value) || double.IsInfinity(pt.Value) )
                            val = 998877;

                        output.WriteLine(pt.DateTime.ToString("MM/dd/yyyy")
                            + " " + s.SiteName.PadRight(12).ToUpper()
                            + " " + s.Parameter.PadRight(9).ToUpper()
                            + " " + val.ToString("F2").PadRight(13)
                            + " " + 998877.ToString("F2").PadRight(13));
                    }
                }
          
                output.Close();
        }


    }
}