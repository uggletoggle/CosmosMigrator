using GKOne.CosmosMigrator.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GKOne.CosmosMigrator.UI
{
    public partial class FormMain : Form
    {

        private readonly Migrator _migrator;
        private int totalDatabases = 0;
        public FormMain()
        {
            InitializeComponent();
            _migrator = new Migrator();
            _migrator.DatabaseCompletion += _migrator_DatabaseCompletion;
        }

        private void _migrator_DatabaseCompletion(object sender, EventArgs e)
        {
            if(_migrator.totalDatabases == 0)
            {
                progressBar.Value = totalDatabases;
                MessageBox.Show("Migration Completed", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if(totalDatabases == 0)
            {
                totalDatabases = _migrator.totalDatabases + 1;
                progressBar.Maximum = totalDatabases;
            }
            
            var totalCompleted = (double)_migrator.totalDatabases / (double)(totalDatabases);

            progressBar.Value = (int)( (double)totalDatabases - totalCompleted);
        }

        private void FormMain_Load(object sender, EventArgs e)
        {

        }

        private async void btnMigrate_ClickAsync(object sender, EventArgs e)
        {
            try
            {
                btnMigrate.Enabled = false;
                _migrator.SetCloudClient(txtConnectionString.Text);
                await _migrator.Migrate();
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnMigrate.Enabled = true;
                progressBar.Value = 0;
            }

        }
    }
}
