using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace pangco_datawh_etl
{
    class Globals
    {
        public static NpgsqlConnection postg_con = null;
        public static SqlConnection autocount_con = null;
        public static JObject jsonconfig { get; set; }
        public static string config_passwd { get; set; }
        public static List<string> postg_tablelist = new List<string>();
        public static List<string> autoc_tablelist = new List<string>();
        public static DataTable schema_autoc = null;
        public static DataTable schema_postg = null;
        public static bool stop_iteration = false;
    }
}
