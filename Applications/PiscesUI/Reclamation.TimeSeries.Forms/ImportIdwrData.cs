﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Reclamation.TimeSeries.IDWR;
using Reclamation.Core;
using System.Diagnostics;

namespace Reclamation.TimeSeries.Forms.ImportForms
{
    public partial class ImportIdwrData : Form
    {
        public string parameter, station;
        public DateTime tStart, tEnd;

        public ImportIdwrData()
        {
            InitializeComponent();

            this.timeSelectorBeginEnd1.T1 = DateTime.Now.AddDays(-10);
            this.timeSelectorBeginEnd1.T2 = DateTime.Now.AddDays(-1);
        }


        public DateTime T2
        {
            get { return this.timeSelectorBeginEnd1.T2; }
        }

        public DateTime T1
        {
            get { return timeSelectorBeginEnd1.T1; }
        }


        private void linkLabelIdwrInfo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://idwr.idaho.gov/files/help/Accounting-Time-Series-Users-Manual.pdf");
        }


        private void comboBoxRiverSystems_OnDropDown(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Requesting River Systems API Data...";
            statusStrip1.Refresh();
            this.comboBoxRiverSystems.DataSource = null;
            this.comboBoxRiverSystems.SelectedValue = null;
            this.comboBoxRiverSystems.Items.Clear();
            var dTab = IDWRDailySeries.GetIdwrRiverSystems();
            this.comboBoxRiverSystems.DataSource = dTab;
            this.comboBoxRiverSystems.ValueMember = "River";
            this.comboBoxRiverSystems.DisplayMember = "Name";

            ComboBox senderComboBox = this.comboBoxRiverSystems;
            int width = senderComboBox.DropDownWidth;
            Graphics g = senderComboBox.CreateGraphics();
            Font font = senderComboBox.Font;
            int vertScrollBarWidth =
                (senderComboBox.Items.Count > senderComboBox.MaxDropDownItems)
                ? SystemInformation.VerticalScrollBarWidth : 0;

            int newWidth, maxWidth = 156;
            for (int i = 0; i < dTab.Rows.Count; i++)
            {
                string s = dTab.Rows[i]["Name"].ToString();
                newWidth = (int)g.MeasureString(s, font).Width
                    + vertScrollBarWidth;
                if (maxWidth < newWidth)
                { maxWidth = newWidth; }
            }
            senderComboBox.DropDownWidth = maxWidth;
            toolStripStatusLabel1.Text = "Done!";
        }

        private void comboBoxRiverSystems_SelectedIndexChanged(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Requesting River Sites API Data...";
            statusStrip1.Refresh();
            if (comboBoxRiverSystems.SelectedValue is string)
            {
                this.comboBoxRiverSites.DataSource = null;
                this.comboBoxRiverSites.Items.Clear();
                var dTab = IDWRDailySeries.GetIdwrRiverSites(this.comboBoxRiverSystems.SelectedValue.ToString());
                this.comboBoxRiverSites.DataSource = dTab;
                this.comboBoxRiverSites.ValueMember = "SiteID";
                this.comboBoxRiverSites.DisplayMember = "SiteName";

                ComboBox senderComboBox = this.comboBoxRiverSites;
                int width = senderComboBox.DropDownWidth;
                Graphics g = senderComboBox.CreateGraphics();
                Font font = senderComboBox.Font;
                int vertScrollBarWidth =
                    (senderComboBox.Items.Count > senderComboBox.MaxDropDownItems)
                    ? SystemInformation.VerticalScrollBarWidth : 0;

                int newWidth, maxWidth = 156;
                for (int i = 0; i < dTab.Rows.Count; i++)
                {
                    string s = dTab.Rows[i]["SiteName"].ToString();
                    newWidth = (int)g.MeasureString(s, font).Width
                        + vertScrollBarWidth;
                    if (maxWidth < newWidth)
                    { maxWidth = newWidth; }
                }
                senderComboBox.DropDownWidth = maxWidth;
                toolStripStatusLabel1.Text = "Done!";
                ValidateDates(sender, e);
            }
        }

        private void comboBoxRiverSites_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxRiverSites.SelectedValue is string)
            {
                toolStripStatusLabel1.Text = "Requesting Site Information API Data...";
                statusStrip1.Refresh();
                var dTab = IDWRDailySeries.GetIdwrSiteInfo(this.comboBoxRiverSites.SelectedValue.ToString());

                switch (dTab.Rows[0]["SiteType"].ToString())
                {
                    case "F":
                    case "Y":
                    case "E":
                    case "W":
                    case "P":
                        {
                            this.radioButtonGH.Enabled = false;
                            this.radioButtonFB.Enabled = false;
                            this.radioButtonAF.Enabled = false;
                            this.radioButtonQD.Enabled = true;
                            this.radioButtonQD.Checked = true;
                            this.buttonOK.Enabled = true;
                            break;
                        }
                    case "D":
                        {
                            this.radioButtonGH.Enabled = true;
                            this.radioButtonFB.Enabled = false;
                            this.radioButtonAF.Enabled = false;
                            this.radioButtonQD.Enabled = true;
                            this.radioButtonQD.Checked = true;
                            this.buttonOK.Enabled = true;
                            break;
                        }
                    case "R":
                        {
                            this.radioButtonGH.Enabled = false;
                            this.radioButtonFB.Enabled = true;
                            this.radioButtonAF.Enabled = true;
                            this.radioButtonQD.Enabled = false;
                            this.radioButtonFB.Checked = true;
                            this.buttonOK.Enabled = true;
                            break;
                        }
                    default:
                        {
                            this.radioButtonGH.Enabled = false;
                            this.radioButtonFB.Enabled = false;
                            this.radioButtonAF.Enabled = false;
                            this.radioButtonQD.Enabled = false;
                            this.radioButtonFB.Checked = false;
                            this.buttonOK.Enabled = false;
                            break;
                        }
                }
                this.labelName.Text = "Name: " + dTab.Rows[0]["FullName"].ToString();
                //this.labelSID.Text = "Site ID: " + dTab.Rows[0]["SiteID"].ToString();
                this.textBoxSID.Text = dTab.Rows[0]["SiteID"].ToString();
                this.labelYears.Text = "Years Available: " + dTab.Rows[0]["Years"].ToString();
                this.labelSType.Text = "Site Type: " + dTab.Rows[0]["SiteType"].ToString();
                toolStripStatusLabel1.Text = "Done!";
                statusStrip1.Refresh();
                ValidateDates(sender, e);
            }
        }

        private void idwrOkButton_Click(object sender, EventArgs e)
        {
            if (this.textBoxSID.Text == "")
            {
                MessageBox.Show("Input a valid Site ID or select a Site from the drop-down lists...");
                this.DialogResult = DialogResult.Ignore;
            }
            else
            {
                toolStripStatusLabel1.Text = "Requesting Site Time Series API Data...";
                this.station = this.textBoxSID.Text;
                this.parameter = "";
                if (this.radioButtonAF.Checked) { parameter = "AF"; }
                if (this.radioButtonFB.Checked) { parameter = "FB"; }
                if (this.radioButtonGH.Checked) { parameter = "GH"; }
                if (this.radioButtonQD.Checked) { parameter = this.textBoxSID.Text; }

                this.tStart = timeSelectorBeginEnd1.T1;
                this.tEnd = timeSelectorBeginEnd1.T2;

                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                toolStripStatusLabel1.Text = "Done!";
            }
        }

        private void ValidateDates(object sender, EventArgs e)
        {
            this.tStart = timeSelectorBeginEnd1.T1;
            this.tEnd = timeSelectorBeginEnd1.T2;

            var years = this.labelYears.Text;

            if (!years.Contains(tStart.Year.ToString()) || !years.Contains(tEnd.Year.ToString()))
            {
                toolStripStatusLabel1.Text = "Selected date range have no data...";
                statusStrip1.Refresh();
            }
        }


    }
}
