using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CredentialManagerViewer
{
    public partial class MainForm : Form
    {
        private List<CredentialInfo> allCredentials = new List<CredentialInfo>();

        public MainForm()
        {
            InitializeComponent();
            SetPlaceholder();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Initialize the DataGridView
            dataGridViewCredentials.Columns.Add("Target", "Target");
            dataGridViewCredentials.Columns.Add("Username", "Username");
            dataGridViewCredentials.Columns.Add("Password", "Password (Base64)");
            dataGridViewCredentials.Columns.Add("Type", "Type");
            dataGridViewCredentials.Columns.Add("PersistType", "Persist Type");
            dataGridViewCredentials.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewCredentials.CellClick += dataGridViewCredentials_CellClick;

            // Refresh credentials on load
            RefreshCredentials();
        }

        private void SetPlaceholder()
        {
            txtSearch.Text = "Search your target here...";
            txtSearch.ForeColor = Color.Gray;
        }
        private void FilterCredentials(string filterText)
        {
            dataGridViewCredentials.Rows.Clear();

            // Apply filter
            var filteredCredentials = string.IsNullOrWhiteSpace(filterText)
                ? allCredentials
                : allCredentials.Where(c => c.Target?.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Add filtered credentials to grid
            foreach (var credential in filteredCredentials)
            {
                dataGridViewCredentials.Rows.Add(
                    credential.Target,
                    credential.UserName,
                    credential.Password,
                    credential.Type,
                    credential.Persist
                );
            }

            // Update status
            lblStatus.Text = $"Showing {filteredCredentials.Count} of {allCredentials.Count} credentials";
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (txtSearch.Text != "Search your target here..." && !string.IsNullOrEmpty(txtSearch.Text))
            {
                FilterCredentials(txtSearch.Text);
            }
            else
            {
                RefreshCredentials();
            }
            
                
        }

        private void txtSearch_Enter(object sender, EventArgs e)
        {
            if (txtSearch.Text == "Search your target here...")
            {
                txtSearch.Text = "";
                txtSearch.ForeColor = Color.Black;
            }
        }

        private void txtSearch_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtSearch.Text))
            {
                SetPlaceholder();
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshCredentials();
        }

        private void RefreshCredentials(string filterText = null)
        {
            if (dataGridViewCredentials.Columns.Count == 0)
                return;

            dataGridViewCredentials.Rows.Clear();

            allCredentials = EnumerateCredentials();

            // Add filter
            var filteredCredentials = string.IsNullOrWhiteSpace(filterText)
                ? allCredentials
                : allCredentials.Where(c => c.Target?.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (string.IsNullOrWhiteSpace(filterText))
            {
                lblStatus.Text = "Showing all credentials";
            }
            else 
            {
                lblStatus.Text = $"Showing {filteredCredentials.Count} of {allCredentials.Count} credentials";
            }


            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            foreach (var credential in filteredCredentials)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.CreateCells(dataGridViewCredentials);
                row.Cells[0].Value = credential.Target;
                row.Cells[1].Value = credential.UserName;
                row.Cells[2].Value = credential.Password;
                row.Cells[3].Value = credential.Type;
                row.Cells[4].Value = credential.Persist;
                rows.Add(row);
            }
            dataGridViewCredentials.Rows.AddRange(rows.ToArray());
        }

        private List<CredentialInfo> EnumerateCredentials()
        {
            List<CredentialInfo> result = new List<CredentialInfo>();

            int count = 0;
            IntPtr pCredentials = IntPtr.Zero;

            bool enumerated = CredEnumerate(null, 0, out count, out pCredentials);

            if (enumerated && count > 0)
            {
                // Get the pointers to each credential
                IntPtr[] ptrCredList = new IntPtr[count];
                Marshal.Copy(pCredentials, ptrCredList, 0, count);

                // Process each credential
                for (int i = 0; i < count; i++)
                {
                    CREDENTIAL credential = (CREDENTIAL)Marshal.PtrToStructure(ptrCredList[i], typeof(CREDENTIAL));
                    CredentialInfo info = new CredentialInfo();

                    info.Target = Marshal.PtrToStringUni(credential.TargetName);
                    info.UserName = Marshal.PtrToStringUni(credential.UserName);

                    // Extract password only if available
                    if (credential.CredentialBlobSize > 0)
                    {
                        byte[] passwordBytes = new byte[credential.CredentialBlobSize];
                        Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, (int)credential.CredentialBlobSize);
                        // Attempt to convert to string - may not work for all credential formats
                        try
                        {
                            info.Password = Convert.ToBase64String(passwordBytes);
                        }
                        catch
                        {
                            info.Password = "<Unable to decode password>";
                        }
                    }
                    else
                    {
                        info.Password = "<No password>";
                    }

                    info.Type = GetCredentialType(credential.Type);
                    info.Persist = GetPersistType(credential.Persist);

                    result.Add(info);
                }
            }

            // Clean up the allocated memory
            if (pCredentials != IntPtr.Zero)
            {
                CredFree(pCredentials);
            }

            return result;
        }

        private string GetCredentialType(uint type)
        {
            switch (type)
            {
                case CRED_TYPE_GENERIC:
                    return "Generic";
                case CRED_TYPE_DOMAIN_PASSWORD:
                    return "Domain Password";
                case CRED_TYPE_DOMAIN_CERTIFICATE:
                    return "Domain Certificate";
                case CRED_TYPE_DOMAIN_VISIBLE_PASSWORD:
                    return "Domain Visible Password";
                default:
                    return $"Unknown ({type})";
            }
        }

        private string GetPersistType(uint persist)
        {
            switch (persist)
            {
                case CRED_PERSIST_SESSION:
                    return "Session";
                case CRED_PERSIST_LOCAL_MACHINE:
                    return "Local Machine";
                case CRED_PERSIST_ENTERPRISE:
                    return "Enterprise";
                default:
                    return $"Unknown ({persist})";
            }
        }

        #region P/Invoke declarations

        [DllImport("advapi32.dll", EntryPoint = "CredEnumerate", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredEnumerate(string filter, int flags, out int count, out IntPtr pCredentials);

        [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
        private static extern void CredFree(IntPtr cred);

        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_TYPE_DOMAIN_PASSWORD = 2;
        private const int CRED_TYPE_DOMAIN_CERTIFICATE = 3;
        private const int CRED_TYPE_DOMAIN_VISIBLE_PASSWORD = 4;

        private const int CRED_PERSIST_SESSION = 1;
        private const int CRED_PERSIST_LOCAL_MACHINE = 2;
        private const int CRED_PERSIST_ENTERPRISE = 3;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        #endregion

        private class CredentialInfo
        {
            public string Target { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public string Type { get; set; }
            public string Persist { get; set; }
        }
        private void dataGridViewCredentials_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var cellValue = dataGridViewCredentials.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    Clipboard.SetText(cellValue);
                }
            }
        }

        private void btnExportToCSV_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.FileName = "credentials.csv";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName))
                {
                    for (int i = 0; i < dataGridViewCredentials.Columns.Count; i++)
                    {
                        sw.Write(dataGridViewCredentials.Columns[i].HeaderText);
                        if (i < dataGridViewCredentials.Columns.Count - 1)
                            sw.Write(",");
                    }
                    sw.WriteLine();

                    foreach (DataGridViewRow row in dataGridViewCredentials.Rows)
                    {
                        if (!row.IsNewRow) // Skip the new row (if any)
                        {
                            for (int i = 0; i < dataGridViewCredentials.Columns.Count; i++)
                            {
                                sw.Write(row.Cells[i].Value?.ToString()); // Handle null values
                                if (i < dataGridViewCredentials.Columns.Count - 1)
                                    sw.Write(",");
                            }
                            sw.WriteLine();
                        }
                    }
                }
                MessageBox.Show("Credentials exported successfully!", "Success");
            }
        }
    }
}