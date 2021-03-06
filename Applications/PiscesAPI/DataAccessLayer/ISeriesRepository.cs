﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiscesAPI.Models;
using Dapper;
using System.Data;
using MySql.Data;

namespace PiscesAPI.DataAccessLayer
{
    interface ISeriesRepository
    {
    }

    public class SeriesRepository : ISeriesRepository
    {

        public List<SeriesModel.Series> GetSeries(string id = "", bool exactMatch = false)
        {
            IDbConnection db = Database.Connect();
            string sqlString = "select * from seriescatalog where isfolder = 0 ";
            if (id != "")
            {
                if (!exactMatch)
                {
                    sqlString += "and lower(tablename) like '%" + id + "%'";
                }
                else
                {
                    sqlString += "and lower(tablename) = '" + id + "'";
                }
            }
            return (List<SeriesModel.Series>)db.Query<SeriesModel.Series>(sqlString);
        }

        public List<SeriesModel.Series> AddOrUpdateSeries(List<SeriesModel.Series> input)
        {
            // 1. Check if series exists
            // 2a. Insert new timeseriesdata table into DB if series !exists
            // 2b. Update seriescatalog if series exists
            throw new NotImplementedException();
        }

        public List<SeriesModel.Series> DeleteSeries(List<SeriesModel.Series> input)
        {
            // 1. Check if series exists
            // 2. Delete seriescatalog row from seriescatalog
            // 3. Drop timeseriesdata table corresponding to seriescatalog row
            throw new NotImplementedException();
        }

        private string GetInsertSQL()
        {
            return "insert into sitecatalog(" +
                        "siteid," +
                        "description," +
                        "state," +
                        "latitude," +
                        "longitude," +
                        "elevation," +
                        "timezone," +
                        "install," +
                        "horizontal_datum," +
                        "vertical_datum," +
                        "vertical_accuracy," +
                        "elevation_method," +
                        "tz_offset," +
                        "active_flag," +
                        "type," +
                        "responsibility," +
                        "agency_region" +
                    ") values (" +
                        "@siteid," +
                        "@description," +
                        "@state," +
                        "@latitude," +
                        "@longitude," +
                        "@elevation," +
                        "@timezone," +
                        "@install," +
                        "@horizontal_datum," +
                        "@vertical_datum," +
                        "@vertical_accuracy," +
                        "@elevation_method," +
                        "@tz_offset," +
                        "@active_flag," +
                        "@type," +
                        "@responsibility," +
                        "@agency_region" +
                    ")";
        }

        private string GetUpdateSQL()
        {
            return "update sitecatalog set " +
                        "description = @description," +
                        "state = @state," +
                        "latitude = @latitude," +
                        "longitude = @longitude," +
                        "elevation = @elevation," +
                        "timezone = @timezone," +
                        "install = @install," +
                        "horizontal_datum = @horizontal_datum," +
                        "vertical_datum = @vertical_datum," +
                        "vertical_accuracy = @vertical_accuracy," +
                        "elevation_method = @elevation_method," +
                        "tz_offset = @tz_offset," +
                        "active_flag = @active_flag," +
                        "type = @type," +
                        "responsibility = @responsibility," +
                        "agency_region = @agency_region " +
                    "where " +
                        "siteid = @siteid";
        }

        private string GetDeleteSQL()
        {
            return "delete from sitecatalog "+
                    "where " +
                        "siteid = @siteid";
        }


    }

}
