﻿using Reclamation.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Reclamation.TimeSeries
{
    /// <summary>
    /// TimeSeriesImporter Manages importing data,
    /// determines if calculations need to be made, flags data
    /// based on quality limits, and sends alerts/alarms
    /// </summary>
    public class TimeSeriesImporter
    {
        TimeSeriesDatabase m_db;
        DatabaseSaveOptions m_saveOption;
        public TimeSeriesImporter(TimeSeriesDatabase db,
           DatabaseSaveOptions saveOption = DatabaseSaveOptions.UpdateExisting)
        {
            m_db = db;
            //m_routing = routing;
            m_saveOption = saveOption;
        }

        /// <summary>
        /// Imports a single series (used for testing)
        /// </summary>
        /// <param name="s"></param>
        /// <param name="computeDependencies"></param>
        /// <param name="computeDailyEachMidnight"></param>
        internal void Import(Series s, bool computeDependencies = false,
            bool computeDailyEachMidnight = false)
        {
            var sl = new SeriesList();
            sl.Add(s);
            Import(sl, computeDependencies, computeDailyEachMidnight,"test");
        }


        /// <summary>
        /// Imports time series data,
        /// 1) set flags
        /// 2) active alarms  
        /// 3) compute dependent data (same interval)
        /// 4) compute daily data when encountering midnight values
        /// </summary>
        /// <param name="importSeries"></param>
        /// <param name="computeDependencies"></param>
        /// <param name="computeDailyDependencies"></param>
        public void Import(SeriesList importSeries,
            bool computeDependencies = false,
            bool computeDailyDependencies = false,
            string importTag = "data")
        {
            var list = Import(importSeries, computeDependencies, importTag);

            if (computeDailyDependencies)
            {
                DailyCalculations(list, importTag);
            }
        }


        /// <summary>
        /// Imports list of series
        /// </summary>
        /// <param name="importSeries"></param>
        /// <param name="computeDependencies"></param>
        /// <param name="importTag"></param>
        /// <returns>list of series including dependent calculations</returns>
        private SeriesList Import(SeriesList importSeries, bool computeDependencies, string importTag)
        {
            Performance p = new Performance();
            int calculationCount = 0;
            var routingList = new SeriesList();

            foreach (var s in importSeries)
            {
                m_db.Quality.SetFlags(s); // to do, log/email flaged data
                m_db.ImportSeriesUsingTableName(s, m_saveOption);
                CheckForAlarms(s);
                
                routingList.Add(s);
                if (computeDependencies)
                {
                    var z = ComputeDependenciesSameInterval(s);
                    calculationCount += z.Count;
                    foreach (var cs in z)
                    {
                        CheckForAlarms(cs);
                    }
                    routingList.AddRange(z);
                }
            }
            
            Export(importTag, routingList);
            Stats(importSeries.Count(), p.ElapsedSeconds, calculationCount,"instant");
            return routingList;
        }

        private void Stats(int importCount ,double s, int calculationCount, string interval)
        {
            if (importCount <= 1 && calculationCount <= 0)
                return;
            Console.WriteLine("imported " + importCount
                + " series with " + calculationCount + " dependent calculations");
            calculationCount += importCount;
            double speed = calculationCount / s;
            Console.WriteLine("elapsed time = " + s.ToString("F2") + " s  " + (speed).ToString("F2") + " records/s");

            //var speedSeries = new Series("import_speed"); ;

            //speedSeries.Add(DateTime.Now, speed, "# " + calculationCount + " series ("+interval+")");
            //speedSeries.SiteID = "system";
            //m_db.ImportSeriesUsingTableName(speedSeries, m_saveOption);

           
        }


        private void DailyCalculations(SeriesList importSeries, string importTag)
        {
            Logger.WriteLine("DailyCalculations(" + importSeries.Count + " series, " + importTag + ")");
            Performance p = new Performance();
            var dailyCalculationQueue = GetDailyDependentCalculations(importSeries);
            var routingList = new SeriesList();

            
            if (dailyCalculationQueue.Count > 0)
            {
                PerformDailyComputations(importSeries, dailyCalculationQueue, routingList);
            }
            Export(importTag, routingList);

            Stats(0, p.ElapsedSeconds, dailyCalculationQueue.Count, "daily");
        }

        private void CheckForAlarms(Series s)
        {
            try
            {
                string alarmCfg = ConfigurationManager.AppSettings["ProcessAlarms"];
                if (!String.IsNullOrEmpty(alarmCfg) && alarmCfg == "true")
                {
                    m_db.Alarms.Check(s); // check for alarms; send email make phone calls
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine("Error processing alarm ", e);
                Console.WriteLine(e.Message);
            }
        }

        

        /// <summary>
        ///  Daily caluclations are needed if 
        ///  a midnight value is involved, or any data
        ///  before today
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private bool NeedDailyCalc(Series s)
        {
            if (s.Count == 0)
                return false;
            // check for midnight values, and initiate daily calculations.
            if (s.TimeInterval == TimeInterval.Irregular)
            {
                return s.MinDateTime <= DateTime.Now.Date;
            }
            return false;
        }

        /// <summary>
        /// Export data to outgoing directory
        /// </summary>
        /// <param name="importTag"></param>
        /// <param name="routingList"></param>
        private void Export(string importTag, SeriesList routingList)
        {
            SeriesList instantRoute = new SeriesList();
            SeriesList dailyRoute = new SeriesList();
            // route data to other locations.
            foreach (var item in routingList)
            {
                TimeSeriesName tn = new TimeSeriesName(item.Table.TableName);
                item.Parameter = tn.pcode;
                item.SiteID = tn.siteid;
                if (item.TimeInterval == TimeInterval.Irregular)
                    instantRoute.Add(item);
                if (item.TimeInterval == TimeInterval.Daily)
                    dailyRoute.Add(item);
            }
            Logger.WriteLine("Exporting data");
            TimeSeriesTransfer export = new TimeSeriesTransfer(m_db);
            export.Export(instantRoute, importTag, TimeInterval.Irregular);
            export.Export(dailyRoute, importTag, TimeInterval.Daily);
        }

        private static void PerformDailyComputations(SeriesList importSeries,
            List<CalculationSeries> calculationQueue, SeriesList routingList)
        {
            // do Actual Computations now. (in proper order...)
            TimeSeriesDependency td = new TimeSeriesDependency(calculationQueue);

             TimeRange tr;
            bool validRange = TryGetDailyTimeRange(importSeries, out tr,DateTime.Now);

            if (!validRange)
            {
                Console.WriteLine(" time range indicates don't perform calculation.");
                Console.WriteLine(" Current Time:" + DateTime.Now.ToString());
                Console.WriteLine(" Default time range :" + tr.StartDate.ToString() + " " + tr.EndDate.ToString());
                return;
            }

            var sortedCalculations = td.Sort();
            foreach (CalculationSeries cs in sortedCalculations)
            {
                Console.Write(">>> " + cs.Table.TableName + ": " + cs.Expression);
                cs.Calculate(tr.StartDate, tr.EndDate);
                if (cs.Count > 0)
                {
                    routingList.Add(cs);
                    if (cs.CountMissing() > 0)
                        Console.WriteLine(" Missing " + cs.CountMissing() + " records");
                    else
                        Console.WriteLine(" OK");
                }
            }
        }

        /// <summary>
        /// Return range of dates to compute daily data.
        /// prevent computing Daily data for Today.
        /// does the data range cross midnight?
        /// is data coming in from yesterday.
        /// </summary>
        /// <param name="inputSeriesList"></param>
        /// <returns></returns>
        internal static bool TryGetDailyTimeRange(SeriesList inputSeriesList, out TimeRange tr, DateTime today)
        {
            DateTime t1, t2;

           t1 = inputSeriesList.MinDateTime; // 8-16 12:00 am
           t2 = inputSeriesList.MaxDateTime;      // 8-17  12:00 am
           var todayMidnight = today.Date;     // 8-17  12:00 am
            var yesterday = today.AddDays(-1).Date;
           tr = new TimeRange(t1.Date, t2);

            // exactly midnight only (no -range) use yesterday
            if(t1 == todayMidnight
                && t2 == todayMidnight)
            {
                tr = new TimeRange(yesterday, yesterday);
                return true;
            }

           if (t2 < todayMidnight) // yesterday (or eailer data)
               return true;


            //  data spanning across midnight 
            if(t1 < todayMidnight &&  t2 >= todayMidnight  )
            {
                t2=todayMidnight.AddDays(-1);
                tr = new TimeRange(t1.Date, t2);
                return true;
            }


            return false;
        }
        private SeriesList ComputeDependenciesSameInterval(Series s)
        {
            int level = 0;
            SeriesList rval = new SeriesList();
            var calcList = GetDependentsRecursive(s.Table.TableName, s.TimeInterval,ref level);
            if (calcList.Count > 0)
                Logger.WriteLine("Found " + calcList.Count + " " + s.TimeInterval + " calculations to update ");


            TimeSeriesDependency d = new TimeSeriesDependency(calcList);
            var sorted = d.Sort();

            foreach (var item in sorted)
            {
                var cs = item as CalculationSeries;
                cs.Calculate(s.MinDateTime, s.MaxDateTime);
                if (cs.Count > 0)
                    rval.Add(cs);
            }

            return rval;
        }

        private List<CalculationSeries> GetDailyDependentCalculations(SeriesList list)
        {
            Logger.WriteLine("GetDailyDependentCalculations(" + list.Count + " series )");
            
            var rval = new List<CalculationSeries>();
            foreach (var s in list)
            {
                int level = 0;
                if (NeedDailyCalc(s) )
                {  // daily calcs that depend on instant
                    var x = GetDependentsRecursive(s.Table.TableName, TimeInterval.Daily,ref level);
                    if( x.Count >0)
                      Logger.WriteLine("for " + s.Table.TableName + " found " + x.Count + " dependencies (before checking for duplicates)");
                    foreach (var item in x)
                    { // prevent duplicates
                        if (!rval.Any(a => a.Table.TableName == item.Table.TableName))
                        {
                            rval.Add(item);
                            Logger.WriteLine(" --> " + item.Table.TableName);
                        }
                    }
                    
                }
            }
            
          return rval;
        }

        /// <summary>
        /// gets dependents for this series (tablename)
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        private List<CalculationSeries> GetDependentsRecursive(string tableName, TimeInterval interval, ref int level)
        {
            string pad = "".PadRight(level);
            Logger.WriteLine(pad+"GetDependentsRecursive(" + tableName + "," + interval.ToString() + ",depth="+level+")");

            int maxRecursiveCount=20;
            if( level > maxRecursiveCount)
            {
                Logger.WriteLine(pad+"TimeSeriesImporter max recursive level reached. "+tableName+" maxRecursiveCount =" + maxRecursiveCount);
                //level = 0;
                return new List<CalculationSeries>();
            }
            var rval = new List<CalculationSeries>();
            TimeSeriesName tn = new TimeSeriesName(tableName);
            var calcList = GetDependentCalculations(tableName, interval);

            foreach (CalculationSeries c in calcList)
            {
                if (!rval.Any(a => a.Table.TableName == c.Table.TableName))
                {
                    rval.Add(c);
                    Logger.WriteLine(pad + "  " + c.Name + " =" + c.Expression);
                }
                // check recursive
                if (tableName != c.Table.TableName)
                {
                    level++;
                    pad = "".PadRight(level);
                    var x = GetDependentsRecursive(c.Table.TableName, interval, ref level);

                    foreach (var d in x)
                    {
                        if (!rval.Any(a => a.Table.TableName == d.Table.TableName))
                        {
                            rval.Add(d);
                            Logger.WriteLine(pad + "   -- " + d.Name + " = " + d.Expression);
                        }
                    }
                } 
            }
            return rval;
        }

        TimeSeriesDependency m_instantDependencies;
        TimeSeriesDependency m_dailyDependencies;

        /// <summary>
        /// Find calculations that depend on this series (tableName) 
        /// These calculations will have the series as input
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="timeInterval"></param>
        /// <returns></returns>
        List<CalculationSeries> GetDependentCalculations(string tableName, TimeSeries.TimeInterval timeInterval)
        {
            // cache with s_instantDependencies speed up from 174 seconds to 28 seconds (agrimet test)
            if (timeInterval == TimeSeries.TimeInterval.Irregular)
            {
                if (m_instantDependencies == null)
                {
                    var rawCalcList = m_db.Factory.GetCalculationSeries(timeInterval, "", "");
                    Logger.WriteLine("Info: GetDependentCalculations, found " + rawCalcList.Count
                           + " calculation series");
                    m_instantDependencies = new TimeSeriesDependency(rawCalcList);
                }
                return m_instantDependencies.LookupCalculations(tableName, timeInterval);
            }
            else if (timeInterval == TimeSeries.TimeInterval.Daily)
            {
                if (m_dailyDependencies == null)
                {
                    var rawCalcList = m_db.Factory.GetCalculationSeries(timeInterval, "", "");
                    m_dailyDependencies = new TimeSeriesDependency(rawCalcList);
                }
                return m_dailyDependencies.LookupCalculations(tableName, timeInterval);
            }

            throw new NotImplementedException("Error: GetDependentCalculations does not support " + timeInterval);

        }

    }
}
