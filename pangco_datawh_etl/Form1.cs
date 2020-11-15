using ExcelDataReader;
using Newtonsoft.Json.Linq;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pangco_datawh_etl
{
    public partial class Form1 : Form
    {
        public DataTable dataSource;

        public Form1()
        {
            InitializeComponent();
            dataSource = new DataTable();
        }

        private bool Connection_Datawarehouse()
        {
            if (Globals.postg_con == null)
            {
                string _host = (string)Globals.jsonconfig["dw_hostname"];
                string _dwuser = (string)Globals.jsonconfig["dw_user"];
                string _dwpass = (string)Globals.jsonconfig["dw_pass"];
                string _dwdatabase = (string)Globals.jsonconfig["dw_database"];
                var cs = "Host=" + _host + ";Username=" + _dwuser + ";Password=" + _dwpass + ";Database=" + _dwdatabase;
                Globals.postg_con = new NpgsqlConnection(cs);
            }
            if (Globals.postg_con.State == ConnectionState.Closed)
            {
                try
                {
                    Globals.postg_con.Open();
                    showMemo("Data Warehouse connected!");
                }
                catch (Exception)
                {
                    showMemo("Fail to connect to Data Warehouse");
                    return false;
                }
            }
            return true;
        }

        private bool Connection_Autocount()
        {
            if (Globals.autocount_con == null)
            {
                string _host = (string)Globals.jsonconfig["ac_hostname"];
                string _folder = (string)Globals.jsonconfig["ac_folder"];
                string _acuser = (string)Globals.jsonconfig["ac_user"];
                string _acpass = (string)Globals.jsonconfig["ac_pass"];
                string _acdatabase = (string)Globals.jsonconfig["ac_database"];
                string _acauthentication = (string)Globals.jsonconfig["ac_windows_authentication"];
                string connetionString = "";

                if (_acauthentication == "0")
                {
                    connetionString = "Data Source=" + _host + @"\" + _folder + "; Initial Catalog=" + _acdatabase + ";User ID=" + _acuser + ";Password=" + _acpass;
                }
                else
                {
                    connetionString = "Data Source=" + _host + @"\" + _folder + "; Initial Catalog=" + _acdatabase + "; Integrated Security=True;";
                }

                Globals.autocount_con = new SqlConnection(connetionString);
            }
            if (Globals.autocount_con.State == ConnectionState.Closed)
            {
                try
                {
                    Globals.autocount_con.Open();
                    showMemo("Autocount connected!");
                }
                catch (Exception)
                {
                    showMemo("Fail to connect to Autocount");
                    return false;
                }
            }
            return true;
        }

        private void close_Datawarehouse()
        {
            if (Globals.postg_con != null)
                if (Globals.postg_con.State == ConnectionState.Open)
                {
                    Globals.postg_con.Close();
                    showMemo("DataWarehouse connection closed!");
                }
        }

        private void close_Autocount()
        {
            if (Globals.autocount_con != null)
                if (Globals.autocount_con.State == ConnectionState.Open)
                {
                    Globals.autocount_con.Close();
                    showMemo("Autocount connection closed!");
                }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("Are you sure to exit application?", "Exit", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                close_Datawarehouse();
                close_Autocount();
            }
            else if (dialogResult == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("Are you sure to exit application?", "Exit", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                Application.Exit();
            }
            else if (dialogResult == DialogResult.No)
            {

            }
        }

        private void configToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConfigPassForm configPassForm = new ConfigPassForm();
            // Show testDialog as a modal dialog and determine if DialogResult = OK.
            if (configPassForm.ShowDialog(this) == DialogResult.OK)
            {
                // Read the contents of testDialog's TextBox.
                if (configPassForm.textBox1.Text == Globals.config_passwd)
                {
                    Form2 myForm = new Form2();
                    myForm.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Wrong password!");
                }
            }
            configPassForm.Dispose();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!File.Exists(@"config.json"))
            {
                MessageBox.Show("Error! File not found.");
            }
            else
            {
                try
                {
                    string readText = File.ReadAllText(@"config.json");
                    string dec_string = Encrypt.DecryptString(readText);
                    Globals.jsonconfig = JObject.Parse(dec_string);
                    Globals.config_passwd = (string)Globals.jsonconfig["menu_pass"];
                }
                catch (Exception)
                {

                }
            }
        }

        private void showMemo(string _memo)
        {
            richTextBox1.AppendText(DateTime.Now.ToString() + ": " + _memo + Environment.NewLine);
        }

        private void btn_sync_Click(object sender, EventArgs e)
        {
            NpgsqlDataAdapter v_adapter = null;
            SqlDataAdapter sql_adapter = null;
            DataTable v_table = new DataTable();
            DataTable _table1;
            int _currentrow = 0;
            int _count = 0;
            string sql = "", _incsql = "", _invsql = "", _insertsql = "", _upsql = "";
            string _dockey = "";

            Globals.stop_iteration = false;
            Globals.in_sync = true;

            close_Autocount();
            close_Datawarehouse();
            if (Connection_Datawarehouse())
            {
                if (Connection_Autocount())
                {
                    sql = "SELECT * FROM autocount_tables WHERE last_update < now()";

                    v_adapter = new NpgsqlDataAdapter(sql, Globals.postg_con);

                    v_adapter.Fill(v_table);

                    string _tablename, _subtable, _lastdatetime, _sql;
                    string[] _tablename_split, _subtable_split;
                    DateTime dt_tmp;

                    progressBar1.Maximum = v_table.Rows.Count;
                    progressBar1.Value = 1;

                    foreach (DataRow vrow in v_table.Rows)
                    {

                        _tablename = vrow["table_name"].ToString();
                        _tablename_split = _tablename.Split('.');
                        _subtable = vrow["table_column"].ToString();
                        _subtable_split = _subtable.Split('~');
                        dt_tmp = Convert.ToDateTime(vrow["last_update"]);
                        _lastdatetime = dt_tmp.ToString("yyyy-MM-dd HH:mm:ss");

                        showMemo("Processing " + _tablename_split[0]);
                        Application.DoEvents();

                        chk_tablevalid(_tablename);
                        if (_subtable_split.Length == 2)
                        {
                            chk_tablevalid(_subtable_split[1]);
                        }
                        if (_subtable_split.Length == 3)
                        {
                            chk_tablevalid(_subtable_split[1]);
                            chk_tablevalid(_subtable_split[2]);
                        }

                        _table1 = null;
                        _table1 = new DataTable();
                        if (_subtable_split[0] != "NONE")
                            sql = "SELECT * FROM " + _tablename_split[0] + " WHERE " + _subtable_split[0] + " > '" + _lastdatetime + "'";
                        else
                        {
                            sql = "SELECT * FROM " + _tablename_split[0];
                            _insertsql = "DELETE FROM \"" + _tablename_split[0] + "\";";
                            var m_createdb1_cmd = new NpgsqlCommand(_insertsql, Globals.postg_con);

                            m_createdb1_cmd.ExecuteNonQuery();
                        }

                        sql_adapter = new SqlDataAdapter(sql, Globals.autocount_con);

                        sql_adapter.Fill(_table1);

                        if (_table1.Rows.Count > 0)
                        {
                            foreach (DataRow _t1row in _table1.Rows)
                            {
                                _insertsql = "INSERT INTO \"" + _tablename_split[0] + "\" ";
                                _incsql = "(";
                                _invsql = "(";
                                _upsql = "";
                                _count = 0;
                                foreach (DataColumn dc in _table1.Columns)
                                {
                                    if (_count > 0)
                                    {
                                        _incsql = _incsql + ", ";
                                        _invsql = _invsql + ", ";
                                        _upsql = _upsql + ", ";

                                    }
                                    _incsql = _incsql + "\"" + dc.ColumnName + "\"";
                                    if (!_t1row.IsNull(dc.ColumnName))
                                    {
                                        _invsql = _invsql + "'" + _t1row[dc.ColumnName].ToString().Replace("'", "''") + "'";
                                        _upsql = _upsql + "\"" + dc.ColumnName + "\" = '" + _t1row[dc.ColumnName].ToString().Replace("'", "''") + "'";
                                    }
                                    else
                                    {
                                        _invsql = _invsql + "null";
                                        _upsql = _upsql + "\"" + dc.ColumnName + "\" = null";
                                    }

                                    if (_subtable_split[0] != "NONE")
                                    {
                                        if (dc.ColumnName == _tablename_split[1])
                                        {
                                            _dockey = _t1row[dc.ColumnName].ToString();
                                        }
                                    }
                                    _count++;
                                }
                                _incsql = _incsql + ")";
                                _invsql = _invsql + ")";

                                if (_subtable_split[0] != "NONE")
                                {
                                    _insertsql = _insertsql + _incsql + " VALUES " + _invsql + " ON CONFLICT (\"" + _tablename_split[1] + "\") DO UPDATE SET " + _upsql;
                                }
                                else
                                {
                                    _insertsql = _insertsql + _incsql + " VALUES " + _invsql;
                                }
                                var m_createdb1_cmd = new NpgsqlCommand(_insertsql, Globals.postg_con);

                                m_createdb1_cmd.ExecuteNonQuery();

                                // add DTL
                                if (_subtable_split.Length == 2)
                                {
                                    insert_dtldata(_tablename, _subtable_split[1], _dockey);
                                }
                                if (_subtable_split.Length == 3)
                                {
                                    insert_dtldata(_tablename, _subtable_split[1], _dockey);
                                    insert_dtldata(_tablename, _subtable_split[2], _dockey);
                                }

                                if (Globals.stop_iteration)
                                {
                                    showMemo("Sync stop by user! [Sub]");
                                    break;
                                }
                                Application.DoEvents();
                            }
                        }

                        _sql = "UPDATE autocount_tables SET last_update=now() WHERE table_name='" + _tablename + "'";
                        var m_createdb_cmd = new NpgsqlCommand(_sql, Globals.postg_con);
                        m_createdb_cmd.ExecuteNonQuery();

                        _currentrow += 1;
                        progressBar1.Value = _currentrow;
                        Application.DoEvents();
                        if (Globals.stop_iteration)
                        {
                            showMemo("Sync stop by user! [Main]");
                            break;
                        }
                    }
                }
            }
            close_Autocount();
            close_Datawarehouse();
            Globals.in_sync = false;
        }

        private void insert_dtldata(string _master, string _detail, string dockey)
        {
            DataTable _table1 = new DataTable();
            SqlDataAdapter sql_adapter;

            string sql = "", _insertsql = "", _incsql = "", _invsql = "", _upsql = "";
            int _count = 0;
            string dtlkey = "";

            string[] _master_split = _master.Split('.');
            string[] _detail_split = _detail.Split('.');

            sql = "SELECT * FROM " + _detail_split[0] + " WHERE " + _master_split[1] + " = '" + dockey + "'";

            sql_adapter = new SqlDataAdapter(sql, Globals.autocount_con);

            sql_adapter.Fill(_table1);

            if (_table1.Rows.Count > 0)
            {
                foreach (DataRow _t1row in _table1.Rows)
                {
                    _insertsql = "INSERT INTO \"" + _detail_split[0] + "\" ";
                    _incsql = "(";
                    _invsql = "(";
                    _upsql = "";
                    _count = 0;
                    foreach (DataColumn dc in _table1.Columns)
                    {
                        if (_count > 0)
                        {
                            _incsql = _incsql + ", ";
                            _invsql = _invsql + ", ";
                            _upsql = _upsql + ", ";

                        }
                        _incsql = _incsql + "\"" + dc.ColumnName + "\"";
                        if (!_t1row.IsNull(dc.ColumnName))
                            _invsql = _invsql + "'" + _t1row[dc.ColumnName].ToString().Replace("'", "''") + "'";
                        else
                            _invsql = _invsql + "null";
                        if (!_t1row.IsNull(dc.ColumnName))
                            _upsql = _upsql + "\"" + dc.ColumnName + "\" = '" + _t1row[dc.ColumnName].ToString().Replace("'", "''") + "'";
                        else
                            _upsql = _upsql + "\"" + dc.ColumnName + "\" = null";

                        if (dc.ColumnName == _detail_split[1])
                        {
                            dtlkey = _t1row[dc.ColumnName].ToString();
                        }

                        _count++;
                    }
                    _incsql = _incsql + ")";
                    _invsql = _invsql + ")";

                    _insertsql = _insertsql + _incsql + " VALUES " + _invsql + " ON CONFLICT (\"" + _detail_split[1] + "\") DO UPDATE SET " + _upsql;
                    var m_createdb1_cmd = new NpgsqlCommand(_insertsql, Globals.postg_con);

                    m_createdb1_cmd.ExecuteNonQuery();

                    if (Globals.stop_iteration)
                    {
                        showMemo("Sync stop by user! [insert_dtldata]");
                        break;
                    }
                    Application.DoEvents();
                }
            }
        }

        private void chk_tablevalid(string _tablename)
        {
            int _count = 0;
            string _sql, _colname, _coldatatype, _condatatype;
            string[] _tablename_split = _tablename.Split('.');

            if (FindTable_autoc(_tablename_split[0]))
            {
                getTableSchema(_tablename_split[0], true);
                if (FindTable_postg(_tablename_split[0]))
                {
                    getTableSchema(_tablename_split[0], false);
                }
                else
                {
                    // No table found in datawarehouse, so create new table and insert record
                    _sql = "CREATE TABLE \"" + _tablename_split[0] + "\"(";
                    _count = 0;
                    foreach (DataRow row in Globals.schema_autoc.Rows)
                    {
                        _colname = row["ColumnName"].ToString();
                        _coldatatype = row["DataTypeName"].ToString();
                        _condatatype = convertToPostg(_coldatatype);
                        if (_count > 0)
                        {
                            _sql = _sql + ", ";
                        }

                        if (_tablename_split.Length > 1)
                        {
                            if (_colname == _tablename_split[1])
                                _sql = _sql + "\"" + _colname + "\" " + _condatatype + " primary key";
                            else
                                _sql = _sql + "\"" + _colname + "\" " + _condatatype;
                        }
                        else
                        {
                            _sql = _sql + "\"" + _colname + "\" " + _condatatype;
                        }

                        _count++;
                    }
                    _sql = _sql + ")";
                    var m_createdb_cmd = new NpgsqlCommand(_sql, Globals.postg_con);
                    m_createdb_cmd.ExecuteNonQuery();
                }
            }
        }

        private string convertToPostg(string _cold)
        {
            switch (_cold)
            {
                case "bit":
                    return "boolean";
                case "char":
                    return "text";
                case "datetime":
                    return "timestamp";
                case "decimal":
                    return "numeric(10, 2)";
                case "ntext":
                    return "text";
                case "nvarchar":
                    return "text";
                case "tinyint":
                    return "smallint";
                case "varbinary":
                    return "bytea";
                case "varchar":
                    return "text";
                case "uniqueidentifier":
                    return "text";
                default:
                    return _cold;
            }
        }

        private void btn_unilever_Click(object sender, EventArgs e)
        {
            DataRow[] result;
            int _currentrow = 0;
            string _filename = "";
            bool _continue = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!String.IsNullOrEmpty(openFileDialog1.FileName))
                {
                    _filename = Path.GetFileName(openFileDialog1.FileName);
                    showMemo("Start loading " + _filename + " to temporary table!");
                    Application.DoEvents();

                    string unilevel_file = openFileDialog1.FileName;
                    dataSource.Reset();
                    using (var stream = File.Open(openFileDialog1.FileName, FileMode.Open, FileAccess.Read))
                    {
                        using (var reader = ExcelReaderFactory.CreateReader(stream))
                        {
                            var conf = new ExcelDataSetConfiguration
                            {
                                UseColumnDataType = true,

                                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                                {
                                    UseHeaderRow = true,
                                    ReadHeaderRow = (rowReader) =>
                                    {
                                        // F.ex skip the first row and use the 2nd row as column headers:
                                        rowReader.Read();
                                    }
                                }
                            };
                            var dataSet = reader.AsDataSet(conf);
                            dataSource = dataSet.Tables[0];

                            showMemo("Excel loaded in to temporary table!");
                            Application.DoEvents();
                        }
                    }
                    result = null;
                    result = dataSource.Select();
                    if (Connection_Datawarehouse())
                    {
                        progressBar1.Maximum = result.Length;
                        progressBar1.Value = 1;

                        using (var cmd = new NpgsqlCommand("INSERT INTO unilever_inv (invtype, docno, docdate, debtorcode, salesagent, prodname, prodcode, taxableamt, amount) SELECT @a, @b, @c, @d, @e, @f, @g, @h, @i WHERE NOT EXISTS (SELECT id FROM unilever_inv WHERE docno=@b AND prodcode=@g);", Globals.postg_con))
                        {
                            var p_a = new NpgsqlParameter("a", DbType.String); // Adjust DbType according to type
                            cmd.Parameters.Add(p_a);
                            var p_b = new NpgsqlParameter("b", DbType.String); // Adjust DbType according to type
                            cmd.Parameters.Add(p_b);
                            var p_c = new NpgsqlParameter("c", DbType.Date); // Adjust DbType according to type
                            cmd.Parameters.Add(p_c);
                            var p_d = new NpgsqlParameter("d", DbType.String); // Adjust DbType according to type
                            cmd.Parameters.Add(p_d);
                            var p_e = new NpgsqlParameter("e", DbType.String); // Adjust DbType according to type
                            cmd.Parameters.Add(p_e);
                            var p_f = new NpgsqlParameter("f", DbType.String); // Adjust DbType according to type
                            cmd.Parameters.Add(p_f);
                            var p_g = new NpgsqlParameter("g", DbType.String); // Adjust DbType according to type
                            cmd.Parameters.Add(p_g);
                            var p_h = new NpgsqlParameter("h", DbType.Decimal); // Adjust DbType according to type
                            cmd.Parameters.Add(p_h);
                            var p_i = new NpgsqlParameter("i", DbType.Decimal); // Adjust DbType according to type
                            cmd.Parameters.Add(p_i);
                            cmd.Prepare();   // This is optional but will optimize the statement for repeated use

                            showMemo("Uploading to DataWarehouse started.");
                            Application.DoEvents();

                            foreach (DataRow row in result)
                            {
                                _continue = true;

                                try
                                {
                                    p_a.Value = Convert.ToString(row["INVTYPE"]);
                                    p_b.Value = Convert.ToString(row["Sales Invoice Tax Number"]);
                                    p_c.Value = Convert.ToDateTime(row["INVH_DATE"]);
                                    p_d.Value = "300" + Convert.ToString(row["Outlet Ref Code"]);
                                    p_e.Value = Convert.ToString(row["Salesman Name(Order Booker)"]);
                                    p_f.Value = Convert.ToString(row["Prod Name"]);
                                    p_g.Value = Convert.ToString(row["Prod Code"]);
                                    p_h.Value = Convert.ToDecimal(row["Net Amount"]);
                                    p_i.Value = Convert.ToDecimal(row["NIV_AFT_GST"]);
                                }
                                catch
                                {
                                    _continue = false;
                                }
                                
                                if(_continue)
                                    cmd.ExecuteNonQuery();
                                
                                _currentrow += 1;
                                if (_currentrow % 10 == 0)
                                {
                                    progressBar1.Value = _currentrow;
                                    Application.DoEvents();
                                }
                            }
                            progressBar1.Value = _currentrow;

                            showMemo("Uploading " + _filename + " to DataWarehouse ended.");
                            Application.DoEvents();
                        }
                    }
                    showMemo("Loading " + _filename + "into datawarehouse successful!");
                    close_Datawarehouse();
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            richTextBox1.Clear();
        }

        private bool testlocal()
        {
            if (Globals.autocount_con == null)
            {
                var connetionString = @"Data Source=(localdb)\MSSQLLocalDB; Integrated Security=True;";

                Globals.autocount_con = new SqlConnection(connetionString);
            }
            if (Globals.autocount_con.State == ConnectionState.Closed)
            {
                try
                {
                    Globals.autocount_con.Open();
                    showMemo("Autocount connected!");
                }
                catch (Exception)
                {
                    showMemo("Fail to connect to Autocount");
                    return false;
                }
            }
            return true;
        }

        private void initializeDataWarehouseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int _currentrow = 0;
            ConfigPassForm configPassForm = new ConfigPassForm();
            // Show testDialog as a modal dialog and determine if DialogResult = OK.
            if (configPassForm.ShowDialog(this) == DialogResult.OK)
            {
                // Read the contents of testDialog's TextBox.
                if (configPassForm.textBox1.Text == Globals.config_passwd)
                {
                    DialogResult dialogResult = MessageBox.Show("Are you sure to initialize Datawarehouse?" + Environment.NewLine + "All data will be lost!", "Exit", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        close_Datawarehouse();
                        if (Connection_Datawarehouse())
                        {
                            var m_createdb_cmd = new NpgsqlCommand(@"DROP SCHEMA public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO postgres; GRANT ALL ON SCHEMA public TO public; COMMENT ON SCHEMA public IS 'standard public schema';", Globals.postg_con);
                            m_createdb_cmd.ExecuteNonQuery();
                            showMemo("Drop all tables!");

                            m_createdb_cmd = new NpgsqlCommand(@"CREATE SEQUENCE public.unilever_inv_id_seq INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1; ALTER SEQUENCE public.unilever_inv_id_seq OWNER TO postgres;", Globals.postg_con);
                            m_createdb_cmd.ExecuteNonQuery();
                            m_createdb_cmd = new NpgsqlCommand(@"CREATE TABLE public.unilever_inv(id integer NOT NULL DEFAULT nextval('unilever_inv_id_seq'::regclass), invtype text, docno text, docdate date, debtorcode text, salesagent text, prodname text, prodcode text, taxableamt numeric(10, 2), amount numeric(10, 2), last_update timestamp DEFAULT now(), CONSTRAINT unilevel_inv_pkey PRIMARY KEY(id)) TABLESPACE pg_default; ALTER TABLE public.unilever_inv OWNER to postgres;", Globals.postg_con);
                            m_createdb_cmd.ExecuteNonQuery();
                            showMemo("Create Table unilever_inv!");

                            m_createdb_cmd = new NpgsqlCommand(@"CREATE TABLE public.autocount_tables(table_name text, table_column text, last_update timestamp without time zone) TABLESPACE pg_default; ALTER TABLE public.autocount_tables OWNER to postgres;", Globals.postg_con);
                            m_createdb_cmd.ExecuteNonQuery();
                            showMemo("Create Table autocount_tables!");

                            if (File.Exists(@"AutocountInit.csv"))
                            {
                                try
                                {
                                    var lines = File.ReadAllLines(@"AutocountInit.csv");

                                    progressBar1.Maximum = lines.Length;
                                    progressBar1.Value = 1;

                                    using (var cmd = new NpgsqlCommand("INSERT INTO autocount_tables (table_name, table_column, last_update) VALUES (@a, @b, @c);", Globals.postg_con))
                                    {
                                        var p_a = new NpgsqlParameter("a", DbType.String);
                                        cmd.Parameters.Add(p_a);
                                        var p_b = new NpgsqlParameter("b", DbType.String);
                                        cmd.Parameters.Add(p_b);
                                        var p_c = new NpgsqlParameter("c", DbType.DateTime);
                                        cmd.Parameters.Add(p_c);

                                        showMemo("Update prefered table list.");
                                        Application.DoEvents();
                                        string[] s_str;
                                        foreach (string line in lines)
                                        {
                                            s_str = line.Split(',');
                                            p_a.Value = s_str[0];
                                            p_b.Value = s_str[1];
                                            p_c.Value = Convert.ToDateTime("01/01/2000");
                                            cmd.ExecuteNonQuery();
                                            _currentrow += 1;
                                            if (_currentrow % 10 == 0)
                                            {
                                                progressBar1.Value = _currentrow;
                                                Application.DoEvents();
                                            }
                                        }
                                        progressBar1.Value = _currentrow;

                                        showMemo("Update prefered table list ended.");
                                        Application.DoEvents();
                                    }
                                }
                                catch (Exception)
                                {

                                }
                            }
                            Globals.postg_con.Close();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Wrong password!");
                }
            }
            configPassForm.Dispose();
        }

        public bool FindTable_postg(string dataT)
        {
            Globals.postg_tablelist.Clear();
            DataTable dt = Globals.postg_con.GetSchema("Tables");
            foreach (DataRow row in dt.Rows)
            {
                string tablename = (string)row[2];
                Globals.postg_tablelist.Add(tablename);
            }
            for (int i = 0; i < Globals.postg_tablelist.Count; i++)
            {
                if (Globals.postg_tablelist[i].Equals(dataT))
                {
                    return true;
                }
            }
            return false;
        }

        public bool FindTable_autoc(string dataT)
        {
            Globals.autoc_tablelist.Clear();
            DataTable dt = Globals.autocount_con.GetSchema("Tables");
            foreach (DataRow row in dt.Rows)
            {
                string tablename = (string)row[2];
                Globals.autoc_tablelist.Add(tablename);
            }
            for (int i = 0; i < Globals.autoc_tablelist.Count; i++)
            {
                if (Globals.autoc_tablelist[i].Contains(dataT))
                {
                    return true;
                }
            }
            return false;
        }

        public bool getTableSchema(string _tablename, bool _autoc)
        {
            SqlCommand cmd_autoc;
            SqlDataReader reader_autoc;

            string sql = @"select * from " + _tablename + " WHERE 1 = 0";
            try
            {
                cmd_autoc = new SqlCommand(sql, Globals.autocount_con);
                reader_autoc = cmd_autoc.ExecuteReader();
                if (_autoc)
                {
                    if (Globals.schema_autoc != null)
                        Globals.schema_autoc.Clear();
                    Globals.schema_autoc = reader_autoc.GetSchemaTable();
                }
                else
                {
                    if (Globals.schema_postg != null)
                        Globals.schema_postg.Clear();
                    Globals.schema_postg = reader_autoc.GetSchemaTable();
                }
                reader_autoc.Close();
            }
            catch (Exception ex)
            {
                showMemo("Error Occurred: " + ex);
                return false;
            }
            return true;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Globals.stop_iteration = true;
            Globals.in_sync = false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now.Hour == 4 && Globals.daily_Sync == false)
            {
                Globals.daily_Sync = true;
                if (!Globals.in_sync)
                    btn_sync_Click(null, null);
            }

            if (DateTime.Now.Hour != 4 && Globals.daily_Sync == true)
            {
                Globals.daily_Sync = false;
            }
        }
    }
}
