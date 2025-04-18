﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.Odbc;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Threading;
using System.Xml.Linq;
using System.Net.Security;


namespace BackUpTool
{
    public partial class Form1 : Form
    {
        OdbcConnection connection;
        String dbLocation;
        Decimal dbSize;
        public Form1()
        {
            InitializeComponent();
        }

        private void setConnection(OdbcConnection conn)
        { 
            this.connection = conn;   
        
        }

        private void runBackUp(String dbname)
        {
           
                //create the backup command
                String backupCommand = $"BACKUP DATABASE {dbname} TO DISK='{dbLocation}'";
                try
                {
                    string odbcName = odbcNameTextBox.Text;
                    string password = passwordTextBox.Text;
                    string user = userName.Text;
                    string connectionString = $"DSN={odbcName};UID={user};PWD={password};";
                    OdbcConnection connection = new OdbcConnection(connectionString);
                    connection.Open();

                    int sessionId;
                    //get session ID
                    using (OdbcCommand getSpid = new OdbcCommand("SELECT @@SPID", connection))
                    {
                        sessionId = Convert.ToInt32(getSpid.ExecuteScalar());
                    }

               
                Task.Run(() => monitorProgress(connectionString, sessionId));

                    OdbcCommand command = new OdbcCommand(backupCommand, connection);
                    command.ExecuteNonQuery();
                    MessageBox.Show("Backup Successfull", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error backing up database: {ex.Message}", "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            

        }

        private void monitorProgress(string connectionString, int sessionID)
        {
            Task.Run(() =>
            {
                using (var connection = new OdbcConnection(connectionString))
                {
                    connection.Open();
                    bool backupInProgress = true;

                    while (backupInProgress)
                    {
                        try
                        {
                            string query = $"SELECT percent_complete FROM sys.dm_exec_requests WHERE session_id = {sessionID} AND command = 'BACKUP DATABASE'";
                            using (var command = new OdbcCommand(query, connection))
                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int percentComplete = Convert.ToInt32(reader["percent_complete"]);
                                    UpdateProgress(percentComplete);
                                    //Console.WriteLine($"Backup Progress: {percentComplete}%");
                                }
                                else
                                {
                                    // Backup is no longer in progress
                                    backupInProgress = false;
                                    UpdateProgress(100); // Set progress bar to 100%
                                }
                            }
                            Thread.Sleep(1000); // Wait 1 sec before polling again
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Monitor error: {ex.Message}");
                            backupInProgress = false;
                        }
                    }
                }
            });
        }




        private void UpdateProgress(int percent)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() => progressBar.Value = percent));
                //Console.WriteLine($"PROGRESS UPDATE IN UPDATE METHOD {percent}");
            }
            else
            {
                progressBar.Value = percent;
                //Console.WriteLine($"PROGRESS UPDATE IN UPDATE METHOD {percent}");
            }
        }

        private void backUp_Click(object sender, EventArgs e)
        {
            //check the selected item in teh combobox
            if (databaseCatalog.SelectedItem == null)
            {
                DialogResult res = MessageBox.Show("Please select a database before proceeding", "No Database Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            String dbname=databaseCatalog.SelectedItem.ToString();
            //check if the connection is not null
            if (connection==null)
            {
                MessageBox.Show("The connection to the server has expired, kindly re connect","Connection Invalid",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }

            //check if the database is not empty
            if (string.IsNullOrEmpty(dbname))
            {
                MessageBox.Show("Please select a database before proceeding", "No Database Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            backUp.Enabled = false;

            //Open the file save dialog to select the DB backup location
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "SQL Server Backup Files (*.bak)|*.bak";
            saveFileDialog.Title = "Select Backup Location";
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            //saveFileDialog.CheckFileExists = true;
            //saveFileDialog.CheckPathExists = true;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.FileName = dbname + ".bak";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            dbLocation = saveFileDialog.FileName;

            //Start the backUp thread
            Task.Run(() => runBackUp(dbname));
            backUp.Enabled = true;




        }

        private void close_Click(object sender, EventArgs e)
        {
            //Close teh application, if task is running terminate 
            DialogResult res = MessageBox.Show("Are you sure you want to exit the application?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if(res==DialogResult.Yes)
            {
                if (connection != null)
                {
                    connection.Close();
                }
                Application.Exit();
            }

        }

        private void connect_Click(object sender, EventArgs e)
        {
            databaseCatalog.Items.Clear();

            //check if the ODBC name and password are not empty
            if (string.IsNullOrEmpty(odbcNameTextBox.Text) || string.IsNullOrEmpty(passwordTextBox.Text) || string.IsNullOrEmpty(userName.Text))
            {
                DialogResult res = MessageBox.Show("Please fill in the connection parameters before proceeding","Not Enough Parameters",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            //use ODBC name and password to connect to the database
            //get the database catalog from the server then load the list
            string odbcName = odbcNameTextBox.Text;
            string password = passwordTextBox.Text;
            string user=userName.Text;
            string connectionString = $"DSN={odbcName};UID={user};PWD={password};";
            try
            {
                using (OdbcConnection connection = new OdbcConnection(connectionString))
                {
                    
                    connection.Open();
                    String dbCatalogQuery = "SELECT NAME FROM SYS.DATABASES ORDER BY NAME";
                    OdbcCommand command = new OdbcCommand(dbCatalogQuery, connection);
                    OdbcDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string dbName = reader.GetString(0);
                        databaseCatalog.Items.Add(dbName);
                    }
                    reader.Close();
                    setConnection(connection);

                    MessageBox.Show("Connection Successfull","Success",MessageBoxButtons.OK,MessageBoxIcon.Information);



                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to database: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }
    }
}
