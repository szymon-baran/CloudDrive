using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;


namespace FileWatcherWinForms
{

    public partial class CloudDrive : Form
    {
        string currentUser;
        string currentTokenUser;

        public CloudDrive()
        {
            InitializeComponent();
            AutoRunOnWindowsStartup();
            WindowAppearance();
            //username.Text = Properties.Settings.Default["usernameForm"].ToString();
        }



        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            bool cursorNotInBar = Screen.GetWorkingArea(this).Contains(Cursor.Position);

            if (this.WindowState == FormWindowState.Minimized && cursorNotInBar)
            {
                this.ShowInTaskbar = false;
                Cloud.Visible = true;
                this.Hide();
            }
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            Cloud.Visible = false;
            this.Visible = true;
        }

        static void SaveLog(string log)
        {

            string docPath = "E:\\ClientFW\\log";
            DateTime thisDay = DateTime.Now;

            if (!Directory.Exists(docPath))
            {
                DirectoryInfo di = Directory.CreateDirectory(docPath);
            }

            using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "LogFileWathcer_" + thisDay.ToString("d").Replace('/', '_') + ".txt"), true))
            {
                outputFile.WriteLine("[" + thisDay.ToString() + "]" + log);
            }
        }

        private async void fileSystemWatcher1_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            //string filename = Path.GetFileName(e.FullPath);
            //RestHelper.DeleteFile(e.FullPath, observedPath.Text, currentTokenUser, currentUser);
            //await RestHelper.UploadFile(e.FullPath, currentTokenUser, filename, observedPath.Text);
            SaveLog($"Changed: {e.FullPath}");

        }

        private async void fileSystemWatcher1_Created(object sender, FileSystemEventArgs e)
        {
            string value = $"Created: {e.FullPath}";

            string filename = Path.GetFileName(e.FullPath);

            await RestHelper.UploadFile(e.FullPath, currentTokenUser, filename, observedPath.Text);
            SaveLog(value);


        }

        private void fileSystemWatcher1_Deleted(object sender, FileSystemEventArgs e)
        {
            RestHelper.DeleteFile(e.FullPath, observedPath.Text, currentTokenUser, currentUser);
            SaveLog($"Deleted: {e.FullPath}");
        }

        private async void fileSystemWatcher1_Renamed(object sender, RenamedEventArgs e)
        {
            //string filename = Path.GetFileName(e.FullPath);
            //RestHelper.DeleteFile(e.FullPath, observedPath.Text, currentTokenUser, currentUser);
            //await RestHelper.UploadFile(e.FullPath, currentTokenUser, filename, observedPath.Text);
            SaveLog($"Renamed:");
            SaveLog($"    Old: {e.OldFullPath}");
            SaveLog($"    New: {e.FullPath}");
        }

        private void fileSystemWatcher1_Error(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private static void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                SaveLog($"Message: {ex.Message}");
                SaveLog("Stacktrace:");
                SaveLog(ex.StackTrace);
                SaveLog(" ");
                PrintException(ex.InnerException);
            }
        }

        private void button_edit_Click(object sender, EventArgs e)
        {
            button_edit.Visible = false;
            button_save.Visible = true;
            observedPath.ReadOnly = false;
        }

        private void button_save_Click(object sender, EventArgs e)
        {
            button_save.Visible = false;
            button_edit.Visible = true;
            observedPath.ReadOnly = true;
            if (!String.IsNullOrEmpty(observedPath.Text))
            {
                Properties.Settings.Default["cloudDriveObserved"] = observedPath.Text;
                fileSystemWatcher1.Path = observedPath.Text;
                fileSystemWatcher1.EnableRaisingEvents = true;
            }
            else
            {
                Properties.Settings.Default["cloudDriveObserved"] = "";
            }
            Properties.Settings.Default.Save();

        }

        private void AutoRunOnWindowsStartup()
        {
            RegistryKey reg = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            reg.SetValue("Cloud Drive", Application.ExecutablePath.ToString());
        }

        private void CloudDrive_Load(object sender, EventArgs e)
        {

        }

        private void WindowAppearance()
        {
            this.BackColor = Color.FromArgb(6, 37, 63);
            button_edit.Visible = false;
            observedPath.Visible = false;
            login.BackColor = Color.FromArgb(89, 159, 216);
            login.ForeColor = Color.FromArgb(29, 29, 29);
            password.PasswordChar = '*';
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            username.Select();
            //this.StartPosition = FormStartPosition.CenterParent;
            if (Convert.ToBoolean(Properties.Settings.Default["rememberMe"]) == true)
            {
                checkBoxRemember.Checked = true;
                username.Text = Properties.Settings.Default["usernameForm"].ToString();
                password.Text = Properties.Settings.Default["passwordForm"].ToString();
                observedPath.Text = Properties.Settings.Default["cloudDriveObserved"].ToString();
                loginUser(username.Text, password.Text);
            }

        }

        private async void checkFile(string token, string username)
        {
            string[] entries = Directory.GetFileSystemEntries(observedPath.Text, "*.*", SearchOption.AllDirectories);
            //string[] newRelativePathEntries = ne;
            List<String> newRelativePathEntries = new List<String>();
            List<String> newRelativePathFromServer = new List<String>();
            var response2 = await RestHelper.GetUserFile(token, username);
            List<FileDTO> convert = JsonConvert.DeserializeObject<List<FileDTO>>(response2) as List<FileDTO>;
            Boolean downloadFile = true;
            foreach (var entry in entries)
            {
                FileAttributes att = File.GetAttributes(entry);
                if (!((att & FileAttributes.Directory) == FileAttributes.Directory))
                {
                    string relativePathCurrentFile = entry.Replace((Properties.Settings.Default["cloudDriveObserved"].ToString() + "\\"), "");
                    newRelativePathEntries.Add(entry);
                }
            }

            foreach (FileDTO file in convert)
            {
                string relativePathFromServer = file.RelativePath.Replace((currentUser + "\\"), "");
                newRelativePathFromServer.Add(relativePathFromServer);
            }

                foreach (FileDTO file in convert)
            {
                string relativePathFromServer = file.RelativePath.Replace((currentUser + "\\"), "");
                foreach (string str in entries)
                {
                    FileAttributes att = File.GetAttributes(str);
                    if (!((att & FileAttributes.Directory) == FileAttributes.Directory))
                    {
                        
                        //Array.Resize(newRelativePathEntries, newRelativePathEntries.Length + 1);
                        //newRelativePathEntries[newRelativePathEntries.Length - 1] = "new string";
                        
                        string relativePathCurrentFile = str.Replace((Properties.Settings.Default["cloudDriveObserved"].ToString() + "\\"), "");
                        if (relativePathFromServer == relativePathCurrentFile)
                        {
                            DateTime modification = File.GetLastWriteTime(str);
                            if (modification < file.CreatedDate)
                            {
                                await RestHelper.DownloadFile(token, file.Id, (Properties.Settings.Default["cloudDriveObserved"].ToString() + "\\") + file.Name);
                            }
                            //downloadFile = false;
                        }
                        //else
                        // {
                        //    downloadFile = true;

                        //}

                    }

                    //int p3 = 0;
                    //string relativePath = filePath.Replace(observedPath, "");
                }
                if (!newRelativePathEntries.Contains(relativePathFromServer))
                {
                    await RestHelper.DownloadFile(token, file.Id, (Properties.Settings.Default["cloudDriveObserved"].ToString() + "\\") + file.Name);
                }
               
                //if (downloadFile == true)
                //{
                //var response4 = await RestHelper.DownloadFile(token, file.Id, (Properties.Settings.Default["cloudDriveObserved"].ToString() + "\\") + file.Name);
                //}
                //else
                //    downloadFile = true;
            }
                //TODO NOT WORKING PUSH SERVER FILE 
            foreach (var entry in newRelativePathEntries)
            {
                if (!newRelativePathFromServer.Contains(entry))
                {
                    string filename = Path.GetFileName(entry);

                    await RestHelper.UploadFile(entry, currentTokenUser, filename, observedPath.Text);
                    //var response4 = await RestHelper.DownloadFile(token, file.Id, (Properties.Settings.Default["cloudDriveObserved"].ToString() + "\\") + file.Name);
                }
            }
        }

        private async void loginUser(string usernamestr, string passwordstr)
        {
            User user = new User();
            UserDTO userDTO = null;
            user.username = usernamestr;
            user.password = passwordstr;
            var response = await RestHelper.Login(user);

            //Debug.WriteLine(response);
            //Debug.WriteLine(Properties.Settings.Default["usernameForm"].ToString());
            if (!response.Contains("Nieprawidłowa nazwa użytkownika"))
            {
                userDTO = System.Text.Json.JsonSerializer.Deserialize<UserDTO>(response);
            }
            //Debug.WriteLine(userDTO.token);
            if (userDTO != null && userDTO.token != null && userDTO.username != null)
            {
                nameApp.Visible = false;
                username.Visible = false;
                password.Visible = false;
                login.Visible = false;
                button_edit.Visible = true;
                observedPath.Visible = true;
                checkBoxRemember.Visible = false;
                fileSystemWatcher1.IncludeSubdirectories = true;
                fileSystemWatcher1.NotifyFilter = NotifyFilters.CreationTime
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size;
                if (String.IsNullOrEmpty(observedPath.Text))
                {
                    fileSystemWatcher1.EnableRaisingEvents = false;
                }
                else
                {
                    fileSystemWatcher1.EnableRaisingEvents = true;
                    // System.IO.DriveInfo di = new System.IO.DriveInfo(observedPath.Text);
                    //System.IO.DirectoryInfo dirInfo = new System.IO.DirectoryInfo(observedPath.Text);
                    //System.IO.FileInfo[] fileNames = dirInfo.GetFiles();
                    //System.IO.DirectoryInfo[] dirInfos = dirInfo.GetDirectories();
                    buttonLogOut.Visible = true;
                    currentUser = userDTO.username;
                    currentTokenUser = userDTO.token;
                    checkFile(userDTO.token, userDTO.username);
                    //int p = 0;
                }
         
                //var res = await RestHelper.UploadFile("E:\\PlikTestowy.txt", userDTO.token);
                //Debug.WriteLine("Witaj");
                //Debug.WriteLine(res);
            }
            else MessageBox.Show(this, "Błędna nazwa użytkownika, lub hasło.");

            if (checkBoxRemember.Checked == true)
            {
                Properties.Settings.Default["rememberMe"] = true;
                Properties.Settings.Default["usernameForm"] = currentUser;
                Properties.Settings.Default["passwordForm"] = password.Text;
                Properties.Settings.Default["usernameForm"] = currentUser;
                //Properties.Settings.Default["tokenForm"] = currentUser;
                Properties.Settings.Default.Save();
            }
            else
            {
                Properties.Settings.Default["rememberMe"] = false;
                Properties.Settings.Default["usernameForm"] = "";
                Properties.Settings.Default["passwordForm"] = "";
                Properties.Settings.Default["usernameForm"] = "";
                //Properties.Settings.Default["tokenForm"] = "";
                Properties.Settings.Default.Save();
            }
            
            //System.IO.FileInfo[] fileNames = dirInfo.GetFiles("*.*");
        }


        private async void login_Click(object sender, EventArgs e)
        {
            loginUser(username.Text, password.Text);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void buttonLogOut_Click(object sender, EventArgs e)
        {
            nameApp.Visible = true;
            username.Visible = true;
            password.Visible = true;
            login.Visible = true;
            button_edit.Visible = false;
            observedPath.Visible = false;
            checkBoxRemember.Visible = true;
            fileSystemWatcher1.EnableRaisingEvents = false;
            currentUser = "";
            currentTokenUser = "";
            buttonLogOut.Visible = false;
        }
    }
}