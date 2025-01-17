﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Compression;

namespace NPS
{
    enum EPkgType
    {
        PKG_TYPE_VITA_APP,
        PKG_TYPE_VITA_DLC,
        PKG_TYPE_VITA_PATCH,
        PKG_TYPE_VITA_PSM,
        PKG_TYPE_VITA_THEME,
        PKG_TYPE_PSP,
        PKG_TYPE_PSP_DLC,
        PKG_TYPE_PSP_UPDATE,
        PKG_TYPE_PSP_THEME,
        PKG_TYPE_PSX,
        PKG_TYPE_PS3_GAME,
        PKG_TYPE_PS3_DLC,
        PKG_TYPE_PS3_DEMO,
        PKG_TYPE_PS3_THEME,
        PKG_TYPE_PS3_AVATAR,
        PKG_TYPE_UNKNOWN,
    }

    [System.Serializable]
    public class DownloadWorker
    {
        public Item currentDownload;
        //private WebClient webClient;
        private DateTime lastUpdate;
        private long lastBytes;
        [System.NonSerialized]
        public ProgressBar progress = new ProgressBar();
        public ListViewItem lvi;
        public int progressValue = 0;
        private EPkgType pkgType;
        private string pkgOutputPath;
        private string pkgOutputDirectory;
        //public bool isRunning { get; private set; }
        //public bool isCompleted { get; private set; }
        //public bool isCanceled { get; private set; }

        public WorkerStatus status { get; private set; }
        [System.NonSerialized]
        Timer timer = new Timer();
        [System.NonSerialized]
        Form formCaller;

        public DownloadWorker(Item itm, Form f)
        {
            currentDownload = itm;
            lvi = new ListViewItem(itm.TitleName);
            lvi.SubItems.Add("Waiting");
            lvi.SubItems.Add("");
            lvi.SubItems.Add("");
            lvi.Tag = this;
            //isRunning = false;
            //isCanceled = false;
            //isCompleted = false;

            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            formCaller = f;
            status = WorkerStatus.Queued;
        }

        public void Recreate(Form formCaller)
        {
            this.formCaller = formCaller;
            progress = new ProgressBar();
            if (progressValue > 100) progressValue = 100;
            progress.Value = progressValue;
            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            lvi.Tag = this;

            if (status == WorkerStatus.Running)
                Start();
            else if (this.status == WorkerStatus.Downloaded)
            {
                Unpack();
            }
            else if (this.status == WorkerStatus.Completed)
            {
                lvi.SubItems[1].Text = "";
                lvi.SubItems[2].Text = "Completed";
            }
        }

        private EPkgType GetPackageType(Item item)
        {
            if( item.ItsPS3 )
            {
                if( item.IsDLC )
                    return EPkgType.PKG_TYPE_PS3_DLC;
                else if( item.IsTheme )
                    return EPkgType.PKG_TYPE_PS3_THEME;
                else if( item.IsAvatar )
                    return EPkgType.PKG_TYPE_PS3_AVATAR;
                else // Game
                    return EPkgType.PKG_TYPE_PS3_GAME;
            }
            else if( item.ItsPS4 )
                return EPkgType.PKG_TYPE_UNKNOWN;
            else if( item.ItsPsp )
            {
                if( item.IsDLC )
                    return EPkgType.PKG_TYPE_PSP_DLC;
                else if( item.IsTheme )
                    return EPkgType.PKG_TYPE_PSP_THEME;
                else if( item.IsUpdate )
                    return EPkgType.PKG_TYPE_PSP_UPDATE;
                else // Game
                    return EPkgType.PKG_TYPE_PSP;
            }
            else if( item.ItsPsx )
                return EPkgType.PKG_TYPE_PSX;
            else // PS Vita
            {
                if( item.IsDLC )
                    return EPkgType.PKG_TYPE_VITA_DLC;
                else if( item.IsTheme )
                    return EPkgType.PKG_TYPE_VITA_THEME;
                else if( item.IsUpdate )
                    return EPkgType.PKG_TYPE_VITA_PATCH;
                else // Game
                    return EPkgType.PKG_TYPE_VITA_APP;
            }
        }

        private void SetDownloadOutputDirectory()
        {
            string relDir = "";
            switch( pkgType )
            {
                case EPkgType.PKG_TYPE_VITA_APP:
                    relDir = "Pkg\\PSV\\APP";
                    break;
                case EPkgType.PKG_TYPE_VITA_DLC:
                    relDir = "Pkg\\PSV\\DLC";
                    break;
                case EPkgType.PKG_TYPE_VITA_PATCH:
                    relDir = "Pkg\\PSV\\PATCH";
                    break;
                case EPkgType.PKG_TYPE_VITA_PSM:
                    relDir = "Pkg\\PSV\\PSM";
                    break;
                case EPkgType.PKG_TYPE_VITA_THEME:
                    relDir = "Pkg\\PSV\\THEME";
                    break;
                case EPkgType.PKG_TYPE_PSP:
                    relDir = String.Format("PSP\\{0}\\PKG", currentDownload.TitleId);
                    break;
                case EPkgType.PKG_TYPE_PSP_DLC:
                    relDir = String.Format("PSP\\{0}\\PKG\\DLC", currentDownload.TitleId);
                    break;
                case EPkgType.PKG_TYPE_PSP_UPDATE:
                    relDir = String.Format( "PSP\\{0}\\PKG\\UPDATE", currentDownload.TitleId);
                    break;
                case EPkgType.PKG_TYPE_PSP_THEME:
                    relDir = "PSP\\THEME\\PKG";
                    break;
                case EPkgType.PKG_TYPE_PSX:
                    relDir = String.Format( "PSX\\{0}\\PKG", currentDownload.TitleId );
                    break;
                case EPkgType.PKG_TYPE_PS3_GAME:
                    relDir = String.Format( "PS3\\{0}\\packages", currentDownload.TitleId );
                    break;
                case EPkgType.PKG_TYPE_PS3_DLC:
                    relDir = String.Format( "PS3\\{0}\\packages", currentDownload.TitleId );
                    break;
                case EPkgType.PKG_TYPE_PS3_DEMO:
                    relDir = String.Format( "PS3\\DEMO\\{0}\\packages", currentDownload.TitleId );
                    break;
                case EPkgType.PKG_TYPE_PS3_THEME:
                    relDir = "PS3\\THEME\\PKG";
                    break;
                case EPkgType.PKG_TYPE_PS3_AVATAR:
                    relDir = "PS3\\AVATAR\\PKG";
                    break;
                case EPkgType.PKG_TYPE_UNKNOWN:
                    relDir = "Pkg\\UNKNOWN";
                    break;
                default:
                    relDir = "Pkg\\UNKNOWN";
                    break;
            }
            pkgOutputDirectory = Path.Combine( Settings.Instance.downloadDir, relDir );
            pkgOutputPath = Path.Combine( pkgOutputDirectory, currentDownload.DownloadFileName + currentDownload.extension );
        }


        public void Start()
        {
            Console.WriteLine("start process " + currentDownload.TitleName);
            timer.Start();
            //isRunning = true;
            status = WorkerStatus.Running;

            pkgType = GetPackageType( currentDownload );
            SetDownloadOutputDirectory();
            if ( !Directory.Exists(pkgOutputDirectory) )
                Directory.CreateDirectory( pkgOutputDirectory );

            Task.Run(() =>
            {
                DownloadFile(currentDownload.pkg, pkgOutputPath);
            });
        }

        public void Cancel()
        {
            timer.Stop();
            if (status == WorkerStatus.Completed) return;

            status = WorkerStatus.Canceled;

            if (smRespStream != null)
                smRespStream.Close();
            if (saveFileStream != null)
                saveFileStream.Close();
            if (unpackProcess != null && !unpackProcess.HasExited)
            {
                unpackProcess.Kill();
            }

            lvi.SubItems[1].Text = "";
            lvi.SubItems[2].Text = "Canceled";
            progressValue = 0;
            progress.Value = progressValue;
            DeletePkg();
        }

        public void Pause()
        {

            if (status == WorkerStatus.Running || status == WorkerStatus.Queued)
            {
                timer.Stop();

                status = WorkerStatus.Paused;

                if (smRespStream != null)
                {
                    smRespStream.Close();
                }
                if (saveFileStream != null)
                    saveFileStream.Close();
                if (unpackProcess != null && !unpackProcess.HasExited)
                {
                    unpackProcess.Kill();
                }

                lvi.SubItems[1].Text = "Paused";
            }
            //progress.Value = 0;
        }

        public void Resume()
        {
            if (status == WorkerStatus.Paused || status == WorkerStatus.DownloadError)
            {
                lvi.SubItems[1].Text = "Queued";
                status = WorkerStatus.Queued;
            }
        }
        public string Pkg { get { return pkgOutputPath; } }

        public void DeletePkg()
        {
            if (currentDownload != null)
            {
                for (int i = 0; i < 1; i++)
                {
                    try
                    {
                        if (File.Exists( pkgOutputPath ))
                        {
                            System.Threading.Thread.Sleep(400);
                            File.Delete( pkgOutputPath );
                        }
                    }
                    catch { i = 5; }
                }
            }
        }

        [System.NonSerialized]
        Process unpackProcess = null;
        public void Unpack()
        {
            if (currentDownload.ItsPS3)
            {
                UnpackPS3();
                return;
            }
            if (currentDownload.ItsCompPack)
            {
                UnpackCompPack();
                return;
            }

            if (this.status == WorkerStatus.Downloaded || this.status == WorkerStatus.Completed)
            {
                lvi.SubItems[2].Text = "Unpacking";

                String tempName = "";
                string dlc = "";
                if (currentDownload.IsDLC == true)
                {
                    //dlc = "[DLC]";
                    tempName = "[DLC] " + currentDownload.ParentGameTitle;
                }
                else tempName = currentDownload.TitleName;

                String fwVersion = "3.60";
                if (tempName.Contains("3.61") /*currentDownload.TitleName.Contains("3.61")*/) fwVersion = "3.61";
                String[] tempStr = tempName.Split();
                tempName = "";

                foreach (var i in tempStr)
                {

                    if ((i.Contains("3.6")) && (!i.Contains("3.61+"))) fwVersion = i;
                    if (!i.Contains("3.6")) tempName += i + " ";

                }


                tempName = Regex.Replace(tempName, "[/:\"*?<>|]+", " ");
                tempName = Regex.Replace(tempName, "\\r\\n", string.Empty);
                tempName = tempName.Trim();

                var replacements = new Dictionary<string, string>
                {
                    ["{pkgfile}"] = "\"" + pkgOutputPath + "\"",
                    ["{titleid}"] = currentDownload.TitleId.Substring(0, 9),
                    ["{gametitle}"] = tempName,
                    ["{region}"] = currentDownload.Region,
                    ["{zrifkey}"] = currentDownload.zRif,
                    ["{fwversion}"] = fwVersion,
                    ["{dlc}"] = dlc,
                    ["  "] = " "
                };

                ProcessStartInfo a = new ProcessStartInfo();
                a.WorkingDirectory = Settings.Instance.downloadDir + Path.DirectorySeparatorChar;
                a.FileName = string.Format("\"{0}\"", Settings.Instance.pkgPath);
                a.WindowStyle = ProcessWindowStyle.Hidden;
                a.CreateNoWindow = true;
                a.Arguments = replacements.Aggregate(Settings.Instance.pkgParams.ToLower(), (str, rep) => str.Replace(rep.Key, rep.Value));
                unpackProcess = new Process();
                unpackProcess.StartInfo = a;

                a.UseShellExecute = false;
                a.RedirectStandardError = true;

                unpackProcess.EnableRaisingEvents = true;
                unpackProcess.Exited += Proc_Exited;
                unpackProcess.ErrorDataReceived += new DataReceivedEventHandler(UnpackProcess_ErrorDataReceived);
                errors = new List<string>();
                unpackProcess.Start();
                unpackProcess.BeginErrorReadLine();
            }
        }

        void UnpackCompPack()
        {
            if (this.status == WorkerStatus.Downloaded || this.status == WorkerStatus.Completed)
            {
                this.status = WorkerStatus.Completed;
                try
                {
                    lvi.SubItems[2].Text = "Processing";

                    //if (Directory.Exists(Path.Combine(Settings.Instance.downloadDir, "rePatch", currentDownload.TitleId)))
                    //    Directory.Delete(Path.Combine(Settings.Instance.downloadDir, "rePatch", currentDownload.TitleId), true);



                    using (var archive = System.IO.Compression.ZipFile.OpenRead(pkgOutputPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Length == 0) continue;

                            string file = Path.Combine(Settings.Instance.downloadDir, "rePatch", currentDownload.TitleId, entry.FullName);
                            var dir = Path.GetDirectoryName(file);

                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            entry.ExtractToFile(file, true);
                        }
                    }

                    //System.IO.Compression.ZipFile.ExtractToDirectory(Path.Combine(Settings.Instance.downloadDir, currentDownload.DownloadFileName + currentDownload.extension), Path.Combine(Settings.Instance.downloadDir, "rePatch", currentDownload.TitleId));
                    lvi.SubItems[1].Text = "";
                    lvi.SubItems[2].Text = "Completed";
                    if (!Settings.Instance.history.completedDownloading.Contains(this.currentDownload))
                        Settings.Instance.history.completedDownloading.Add(this.currentDownload);

                    if (Settings.Instance.deleteAfterUnpack)
                        DeletePkg();
                }
                catch (Exception err)
                {
                    lvi.SubItems[1].Text = "Error!";
                    lvi.SubItems[2].Text = err.Message;
                }
            }
        }

        void UnpackPS3()
        {
            if (this.status == WorkerStatus.Downloaded || this.status == WorkerStatus.Completed)
            {
                this.status = WorkerStatus.Completed;
                try
                {
                    lvi.SubItems[2].Text = "Processing";
                    // jon: custom PS3 file placement
                    string gamePath = Settings.Instance.downloadDir + Path.DirectorySeparatorChar +
                        "PS3" + Path.DirectorySeparatorChar +
                        currentDownload.TitleId;
                    string path = gamePath + Path.DirectorySeparatorChar + "packages";
                    // jon: end
                    if( !Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    //string newPkgName = String.Format( "{0} [{1}] [{2}]", currentDownload.TitleName, currentDownload.TitleId, currentDownload.GetRegionCode() );
                    string newPkgName = currentDownload.ContentId;
                    if( currentDownload.IsDLC )
                        newPkgName = "[DLC] " + newPkgName;
                    if( currentDownload.IsUpdate )
                        newPkgName = "[UPDATE] " + newPkgName;
                    File.Move(pkgOutputPath, path + Path.DirectorySeparatorChar + newPkgName + currentDownload.extension);
                    

                    // jon: changing to custom directory location
                    path = gamePath + Path.DirectorySeparatorChar + "exdata";
                    // jon: end

                    if( !string.IsNullOrEmpty(currentDownload.ContentId) && currentDownload.ContentId.ToLower() != "missing" && currentDownload.zRif.ToLower() != "NOT REQUIRED".ToLower() && currentDownload.zRif.Length % 2 == 0)
                    {
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        byte[] array = new byte[currentDownload.zRif.Length / 2];
                        for (int i = 0; i < currentDownload.zRif.Length / 2; i++)
                        {
                            array[i] = Convert.ToByte(currentDownload.zRif.Substring(i * 2, 2), 16);
                        }

                        File.WriteAllBytes(path + Path.DirectorySeparatorChar + currentDownload.ContentId + ".rap", array);
                    }

                    lvi.SubItems[1].Text = "";
                    lvi.SubItems[2].Text = "Completed";

                    if (!Settings.Instance.history.completedDownloading.Contains(this.currentDownload))
                        Settings.Instance.history.completedDownloading.Add(this.currentDownload);

                }
                catch (Exception err)
                {
                    lvi.SubItems[1].Text = "Error!";
                    lvi.SubItems[2].Text = err.Message;
                }

            }
        }

        List<string> errors = new List<string>();

        private void UnpackProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            errors.Add(e.Data);
        }

        private void Proc_Exited(object sender, EventArgs e)
        {
            this.status = WorkerStatus.Completed;

            var proc = (sender as Process);
            if (proc.ExitCode == 0)
            {
                formCaller.Invoke(new Action(() =>
                {
                    lvi.SubItems[1].Text = "";
                    lvi.SubItems[2].Text = "Completed";

                    if (!Settings.Instance.history.completedDownloading.Contains(this.currentDownload))
                        Settings.Instance.history.completedDownloading.Add(this.currentDownload);

                    if (Settings.Instance.deleteAfterUnpack)
                        DeletePkg();
                }));
            }
            else
            {
                formCaller.Invoke(new Action(() =>
                {
                    lvi.SubItems[1].Text = "PKG decrypt err!";
                    lvi.SubItems[2].Text = "";

                    errors.Remove(null);
                    if (errors.Count > 0)
                    {
                        if (errors[0].Contains("pkg_dec - PS Vita PKG decryptor/unpacker")) errors.Remove(errors[0]);
                        if (errors.Count > 0)
                            lvi.SubItems[2].Text = errors[0];
                    }
                }
                ));
            }
        }


        long totalSize = 0;
        long completedSize = 0;
        [System.NonSerialized]
        System.IO.Stream smRespStream;
        [System.NonSerialized]
        System.IO.FileStream saveFileStream;
        void DownloadFile(string sSourceURL, string sDestinationPath)
        {
            try
            {
                long iFileSize = 0;
                int iBufferSize = 1024;
                iBufferSize *= 1000;
                long iExistLen = 0;

                if (System.IO.File.Exists(sDestinationPath))
                {
                    System.IO.FileInfo fINfo =
                       new System.IO.FileInfo(sDestinationPath);
                    iExistLen = fINfo.Length;
                }
                ;
                if (iExistLen > 0)
                    saveFileStream = new System.IO.FileStream(sDestinationPath,
                      System.IO.FileMode.Append, System.IO.FileAccess.Write,
                      System.IO.FileShare.ReadWrite);
                else
                    saveFileStream = new System.IO.FileStream(sDestinationPath,
                      System.IO.FileMode.Create, System.IO.FileAccess.Write,
                      System.IO.FileShare.ReadWrite);

                HttpWebRequest hwRq;
                System.Net.HttpWebResponse hwRes;
                var urr = new Uri(sSourceURL);
                hwRq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(urr);
                hwRq.Proxy = Settings.Instance.proxy;
                hwRes = (System.Net.HttpWebResponse)hwRq.GetResponse();
                hwRes.Close();

                long totalLength = hwRes.ContentLength;
                totalSize = totalLength;
                if (totalLength != iExistLen)
                {
                    hwRq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(urr);
                    hwRq.Proxy = Settings.Instance.proxy;
                    hwRq.AddRange(iExistLen);

                    hwRes = (System.Net.HttpWebResponse)hwRq.GetResponse();
                    smRespStream = hwRes.GetResponseStream();

                    iFileSize = hwRes.ContentLength;
                    //   totalSize += hwRes.ContentLength;

                    byte[] downBuffer = new byte[iBufferSize];
                    int iByteSize;
                    while ((iByteSize = smRespStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                    {
                        if (status == WorkerStatus.Paused || status == WorkerStatus.Canceled) return;

                        saveFileStream.Write(downBuffer, 0, iByteSize);

                        completedSize = saveFileStream.Position;

                        if (lastBytes == 0)
                        {
                            lastUpdate = DateTime.Now;
                            lastBytes = completedSize;

                        }
                        else
                        {
                            var now = DateTime.Now;
                            var timeSpan = now - lastUpdate;
                            var bytesChange = completedSize - lastBytes;
                            if (timeSpan.Seconds != 0)
                            {
                                bytesPerSecond = bytesChange / timeSpan.Seconds;
                                lastBytes = completedSize;
                                lastUpdate = now;


                            }
                        }
                    }
                    smRespStream.Close();
                }


                saveFileStream.Close();
                formCaller.Invoke(new Action(() => { DownloadCompleted(); }));
            }
            catch (Exception err)
            {

                formCaller.Invoke(new Action(() =>
                {
                    this.Pause();
                    MessageBox.Show("Unable to download \"" + currentDownload.TitleName + "\"." + Environment.NewLine + err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));

            }

        }


        long bytesPerSecond = 0;


        private void DownloadCompleted()
        {
            timer.Stop();

            this.status = WorkerStatus.Downloaded;

            lvi.SubItems[1].Text = "";


            Unpack();

            progressValue = 100;
            progress.Value = progressValue;


        }



        private void Timer_Tick(object sender, EventArgs e)
        {
            string speed = "";
            bytesPerSecond = bytesPerSecond / 1024;
            if (bytesPerSecond < 1500)
                speed = bytesPerSecond.ToString() + " KB/s";
            else
            {
                speed = ((float)((float)bytesPerSecond / 1024)).ToString("0.00") + " MB/s";
            }

            lvi.SubItems[1].Text = speed;
            var prgs = (float)completedSize / (float)totalSize;

            try
            {
                if (prgs != float.NaN)
                {
                    progressValue = Convert.ToInt32(prgs * 100);
                    progress.Value = progressValue;
                }
            }
            catch { }

            lvi.SubItems[2].Text = completedSize / (1024 * 1024) + "MB/" + totalSize / (1024 * 1024) + "MB";
        }
    }

    public enum WorkerStatus { Queued, Running, Paused, Completed, Downloaded, Canceled, DownloadError }
}


