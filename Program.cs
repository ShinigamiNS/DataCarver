using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataCarverGUI
{
    public class FoundFile
    {
        public long Offset { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Size { get; set; }
    }

    public class MainForm : Form
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [StructLayout(LayoutKind.Sequential)]
        public struct NTFS_VOLUME_DATA_BUFFER {
            public long VolumeSerialNumber;
            public long NumberSectors;
            public long TotalClusters;
            public long FreeClusters;
            public long TotalReserved;
            public uint BytesPerSector;
            public uint BytesPerCluster;
            public uint BytesPerFileRecordSegment;
            public uint ClustersPerFileRecordSegment;
            public long MftValidDataLength;
            public long MftStartLcn;
            public long Mft2StartLcn;
            public long MftZoneStart;
            public long MftZoneEnd;
        }

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, out NTFS_VOLUME_DATA_BUFFER lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, ref long lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool GetDiskFreeSpace(string lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING = 3;
        const uint FSCTL_GET_VOLUME_BITMAP = 0x0009003F;
        const uint FSCTL_LOCK_VOLUME = 0x00090018;
        const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;

        static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
        static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46 };
        static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47 };
        static readonly byte[] RiffSignature = { 0x52, 0x49, 0x46, 0x46 }; // RIFF
        static readonly byte[] WebpIdentifier = { 0x57, 0x45, 0x42, 0x50 }; // WEBP
        static readonly byte[] ZipSignature = { 0x50, 0x4B, 0x03, 0x04 };
        static readonly byte[] RarSignature = { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 };
        static readonly byte[] ExeSignature = { 0x4D, 0x5A };
        static readonly byte[] Mp3Signature = { 0x49, 0x44, 0x33 };
        static readonly byte[] Mp4Signature = { 0x66, 0x74, 0x79, 0x70 }; // Offset by 4 bytes usually
        static readonly byte[] AviSignature = { 0x41, 0x56, 0x49, 0x20 }; // Used with RIFF
        static readonly byte[] GifSignature = { 0x47, 0x49, 0x46, 0x38 };
        static readonly byte[] BmpSignature = { 0x42, 0x4D };
        static readonly byte[] WavSignature = { 0x57, 0x41, 0x56, 0x45 }; // Used with RIFF
        static readonly byte[] RtfSignature = { 0x7B, 0x5C, 0x72, 0x74, 0x66 };
        static readonly byte[] SqliteSignature = { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00 };
        static readonly byte[] GzipSignature = { 0x1F, 0x8B, 0x08 };
        static readonly byte[] TarSignature = { 0x75, 0x73, 0x74, 0x61, 0x72 }; // Offset 257
        static readonly byte[] SevenZipSignature = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
        static readonly byte[] ElfSignature = { 0x7F, 0x45, 0x4C, 0x46 };
        static readonly byte[] ClassSignature = { 0xCA, 0xFE, 0xBA, 0xBE };
        static readonly byte[] PsdSignature = { 0x38, 0x42, 0x50, 0x53 };
        static readonly byte[] OggSignature = { 0x4F, 0x67, 0x67, 0x53 };
        static readonly byte[] FlacSignature = { 0x66, 0x4C, 0x61, 0x43 };

        static readonly byte[] PngEof = { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
        static readonly byte[] PdfEof = { 0x25, 0x25, 0x45, 0x4F, 0x46 };

        TabControl tabControl;
        TabPage carverTab;
        TabPage browserTab;
        TabPage cleanerTab;

        ComboBox driveCombo;
        Button scanBtn;
        ComboBox typeFilterCombo;
        ListView resultsListView;
        Button prevPageBtn;
        Button nextPageBtn;
        Label pageLabel;
        PictureBox previewBox;
        Button restoreBtn;
        Label statusLabel;

        TextBox pathBox;
        Button loadDirBtn;
        ListBox existingFilesList;
        PictureBox browserPreviewBox;
        
        ComboBox cleanerDriveCombo;
        Button cleanBtn;
        Label cleanerStatusLabel;

        NumericUpDown scanCacheNumeric;
        NumericUpDown cleanCacheNumeric;

        volatile bool _scanning = false;
        volatile bool _cleaning = false;
        long _cleanedCount = 0;
        List<FoundFile> allFoundFiles = new List<FoundFile>();
        HashSet<string> knownTypes = new HashSet<string>();
        HashSet<long> knownOffsets = new HashSet<long>();
        int currentPage = 1;
        int pageSize = 1000;
        object listLock = new object();
        System.Windows.Forms.Timer uiUpdateTimer;
        int lastUpdatedCount = 0;
        
        public MainForm()
        {
            Text = "Data Carver";
            Size = new Size(820, 640);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = new Icon("logo.ico"); } catch {}

            tabControl = new TabControl { Dock = DockStyle.Fill };
            carverTab = new TabPage("Raw Disk Carver");
            browserTab = new TabPage("Test Normal Files");
            cleanerTab = new TabPage("Data Cleaner");

            tabControl.TabPages.Add(carverTab);
            tabControl.TabPages.Add(browserTab);
            tabControl.TabPages.Add(cleanerTab);
            Controls.Add(tabControl);

            InitializeCarverTab();
            InitializeBrowserTab();
            InitializeCleanerTab();
            CheckAdmin();
        }

        private void InitializeCarverTab()
        {
            driveCombo = new ComboBox { Location = new Point(10, 10), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach(var di in DriveInfo.GetDrives()) {
                driveCombo.Items.Add(di.Name.Substring(0, 1));
            }
            if (driveCombo.Items.Count > 0) driveCombo.SelectedIndex = 0;

            scanBtn = new Button { Text = "Scan Drive", Location = new Point(120, 10), Width = 100 };
            scanBtn.Click += ScanBtn_Click;

            Label scanCacheLabel = new Label { Location = new Point(230, 15), Width = 70, Text = "Cache (MB):" };
            scanCacheNumeric = new NumericUpDown { Location = new Point(300, 10), Width = 60, Minimum = 1, Maximum = 1024, Value = 8 };

            statusLabel = new Label { Location = new Point(370, 15), Width = 400, Text = "Ready. (Run as Admin to scan raw drivers)" };

            Label filterLabel = new Label { Location = new Point(10, 42), Width = 70, Text = "Filter Type:" };
            typeFilterCombo = new ComboBox { Location = new Point(80, 40), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            typeFilterCombo.Items.Add("All");
            typeFilterCombo.SelectedIndex = 0;
            typeFilterCombo.SelectedIndexChanged += TypeFilterCombo_SelectedIndexChanged;

            resultsListView = new ListView { Location = new Point(10, 70), Width = 300, Height = 420, View = View.Details, FullRowSelect = true, GridLines = true };
            resultsListView.Columns.Add("Position", 80);
            resultsListView.Columns.Add("Type", 50);
            resultsListView.Columns.Add("Name", 100);
            resultsListView.Columns.Add("Size", 50);
            resultsListView.SelectedIndexChanged += ResultsListView_SelectedIndexChanged;

            prevPageBtn = new Button { Text = "<", Location = new Point(10, 495), Width = 40 };
            prevPageBtn.Click += PrevPageBtn_Click;

            pageLabel = new Label { Location = new Point(55, 498), Width = 200, Text = "Page 1 / 1", TextAlign = ContentAlignment.MiddleCenter };

            nextPageBtn = new Button { Text = ">", Location = new Point(270, 495), Width = 40 };
            nextPageBtn.Click += NextPageBtn_Click;

            uiUpdateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            uiUpdateTimer.Tick += UiUpdateTimer_Tick;

            previewBox = new PictureBox { 
                Location = new Point(320, 40), 
                Width = 450, 
                Height = 440, 
                SizeMode = PictureBoxSizeMode.Zoom, 
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            restoreBtn = new Button { Text = "Restore Selected", Location = new Point(320, 490), Width = 150 };
            restoreBtn.Click += RestoreBtn_Click;

            carverTab.Controls.Add(driveCombo);
            carverTab.Controls.Add(scanBtn);
            carverTab.Controls.Add(scanCacheLabel);
            carverTab.Controls.Add(scanCacheNumeric);
            carverTab.Controls.Add(statusLabel);
            carverTab.Controls.Add(filterLabel);
            carverTab.Controls.Add(typeFilterCombo);
            carverTab.Controls.Add(resultsListView);
            carverTab.Controls.Add(prevPageBtn);
            carverTab.Controls.Add(pageLabel);
            carverTab.Controls.Add(nextPageBtn);
            carverTab.Controls.Add(previewBox);
            carverTab.Controls.Add(restoreBtn);
        }

        private void TypeFilterCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            lock (listLock) {
                currentPage = 1;
                UpdateListView();
            }
        }

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            lock (listLock) {
                if (allFoundFiles.Count != lastUpdatedCount) {
                    lastUpdatedCount = allFoundFiles.Count;
                    UpdateListView();
                }
            }
        }

        private void UpdateListView()
        {
            string filter = typeFilterCombo.SelectedItem != null ? typeFilterCombo.SelectedItem.ToString() : "All";
            
            foreach (var t in knownTypes) {
                if (!typeFilterCombo.Items.Contains(t))
                    typeFilterCombo.Items.Add(t);
            }

            var filteredFiles = filter == "All" ? allFoundFiles : allFoundFiles.Where(f => f.Type == filter).ToList();

            int totalPages = (filteredFiles.Count + pageSize - 1) / pageSize;
            if (totalPages == 0) totalPages = 1;
            if (currentPage > totalPages) currentPage = totalPages;

            pageLabel.Text = string.Format("Page {0} / {1}", currentPage, totalPages);

            int start = (currentPage - 1) * pageSize;
            int numItems = Math.Min(pageSize, filteredFiles.Count - start);

            resultsListView.BeginUpdate();
            resultsListView.Items.Clear();
            for (int i = 0; i < numItems; i++)
            {
                var f = filteredFiles[start + i];
                ListViewItem item = new ListViewItem(f.Offset.ToString());
                item.SubItems.Add(f.Type);
                item.SubItems.Add(f.Name);
                item.SubItems.Add(f.Size);
                item.Tag = f;
                resultsListView.Items.Add(item);
            }
            resultsListView.EndUpdate();
            if (_scanning) {
                statusLabel.Text = string.Format("Scanning... Total files found so far: {0}", allFoundFiles.Count);
            }
        }

        private void PrevPageBtn_Click(object sender, EventArgs e) {
            lock (listLock) {
                if (currentPage > 1) { currentPage--; UpdateListView(); }
            }
        }

        private void NextPageBtn_Click(object sender, EventArgs e) {
            lock (listLock) {
                string filter = typeFilterCombo.SelectedItem != null ? typeFilterCombo.SelectedItem.ToString() : "All";
                int count = filter == "All" ? allFoundFiles.Count : allFoundFiles.Count(f => f.Type == filter);
                int totalPages = (count + pageSize - 1) / pageSize;
                if (currentPage < totalPages) { currentPage++; UpdateListView(); }
            }
        }

        private void InitializeBrowserTab()
        {
            pathBox = new TextBox { Location = new Point(10, 10), Width = 600, Text = "C:\\" };
            loadDirBtn = new Button { Text = "Load Folder", Location = new Point(620, 8), Width = 100 };
            loadDirBtn.Click += LoadDirBtn_Click;

            existingFilesList = new ListBox { Location = new Point(10, 40), Width = 300, Height = 480 };
            existingFilesList.SelectedIndexChanged += ExistingFilesList_SelectedIndexChanged;

            browserPreviewBox = new PictureBox { 
                Location = new Point(320, 40), 
                Width = 450, 
                Height = 440, 
                SizeMode = PictureBoxSizeMode.Zoom, 
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            browserTab.Controls.Add(pathBox);
            browserTab.Controls.Add(loadDirBtn);
            browserTab.Controls.Add(existingFilesList);
            browserTab.Controls.Add(browserPreviewBox);
        }

        private void CheckAdmin()
        {
            bool isAdmin;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            if (!isAdmin)
            {
                statusLabel.Text = "Warning: No Admin rights detected. Scanning will fail.";
                statusLabel.ForeColor = Color.Red;
            }
        }

        private void LoadDirBtn_Click(object sender, EventArgs e)
        {
            existingFilesList.Items.Clear();
            try {
                string[] files = Directory.GetFiles(pathBox.Text);
                foreach(string file in files) {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp") {
                        existingFilesList.Items.Add(file);
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show("Error loading directory: " + ex.Message);
            }
        }
        
        private Image CreatePlaceholderMessage(string message) {
            Bitmap bmp = new Bitmap(450, 440);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.Clear(Color.FromArgb(40, 40, 40));
                g.DrawString(message, new Font("Segoe UI", 12), Brushes.White, new RectangleF(10, 150, 430, 200));
            }
            return bmp;
        }

        private void ExistingFilesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (existingFilesList.SelectedItem != null)
            {
                string filePath = existingFilesList.SelectedItem.ToString();
                try {
                    // Check if it's actually WEBP disguised as JPG
                    byte[] header = new byte[12];
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
                        fs.Read(header, 0, 12);
                    }
                    if (Match(header, 0, RiffSignature) && Match(header, 8, WebpIdentifier)) {
                        Image old = browserPreviewBox.Image;
                        browserPreviewBox.Image = CreatePlaceholderMessage("WebP Compression Detected.\n\nBuilt-in Windows GDI+ component cannot visually render WebP.\n(A modern web browser opens it fine, but this app is lightweight).\n\nIf you carve this format from raw disk, you CAN restore it perfectly though!");
                        if (old != null) old.Dispose();
                        return;
                    }

                    Image img = Image.FromFile(filePath);
                    Image oldImg = browserPreviewBox.Image;
                    browserPreviewBox.Image = (Image)img.Clone();
                    img.Dispose();
                    if (oldImg != null) oldImg.Dispose();
                } catch {
                    browserPreviewBox.Image = CreatePlaceholderMessage("Format currently unsupported or decoding failed."); 
                }
            }
        }

        private void ScanBtn_Click(object sender, EventArgs e)
        {
            if (_scanning) {
                _scanning = false;
                scanBtn.Text = "Stopping...";
                return;
            }

            if (driveCombo.SelectedItem == null) return;
            string drive = driveCombo.SelectedItem.ToString();
            string volumePath = "\\\\.\\" + drive + ":";

            allFoundFiles.Clear();
            knownTypes.Clear();
            knownOffsets.Clear();
            lastUpdatedCount = 0;
            currentPage = 1;
            resultsListView.Items.Clear();
            typeFilterCombo.Items.Clear();
            typeFilterCombo.Items.Add("All");
            typeFilterCombo.SelectedIndex = 0;
            uiUpdateTimer.Start();
            
            _scanning = true;
            scanBtn.Text = "Stop scan";
            statusLabel.Text = "Scanning " + volumePath + "...";
            statusLabel.ForeColor = Color.Black;

            int cacheSizeMB = (int)scanCacheNumeric.Value;

            ThreadPool.QueueUserWorkItem((state) => {
                ScanDisk(volumePath, cacheSizeMB);
            });
        }

        private void ProcessBuffer(byte[] buffer, int length, long baseOffset)
        {
            int numThreads = Environment.ProcessorCount;
            if (numThreads < 1) numThreads = 1;
            int chunkSize = length / numThreads;

            Parallel.For(0, numThreads, t =>
            {
                int start = t * chunkSize;
                int end = (t == numThreads - 1) ? length - 12 : start + chunkSize;

                for (int i = start; i < end; i++)
                {
                    if (!_scanning) break;

                    int actualOffset = i;
                    string type = null;
                    if (Match(buffer, i, JpegSignature)) type = "jpg";
                    else if (Match(buffer, i, ZipSignature)) type = "zip";
                    else if (Match(buffer, i, PdfSignature)) type = "pdf";
                    else if (Match(buffer, i, PngSignature)) type = "png";
                    else if (Match(buffer, i, ExeSignature)) type = "exe";
                    else if (Match(buffer, i, RarSignature)) type = "rar";
                    else if (Match(buffer, i, Mp3Signature)) type = "mp3";
                    else if (Match(buffer, i, RtfSignature)) type = "rtf";
                    else if (Match(buffer, i, GifSignature)) type = "gif";
                    else if (Match(buffer, i, SqliteSignature)) type = "sqlite";
                    else if (Match(buffer, i, GzipSignature)) type = "gz";
                    else if (Match(buffer, i, SevenZipSignature)) type = "7z";
                    else if (Match(buffer, i, ElfSignature)) type = "elf";
                    else if (Match(buffer, i, PsdSignature)) type = "psd";
                    else if (Match(buffer, i, OggSignature)) type = "ogg";
                    else if (Match(buffer, i, FlacSignature)) type = "flac";
                    else if (Match(buffer, i, ClassSignature)) type = "class";
                    else if (Match(buffer, i, RiffSignature))
                    {
                        if (Match(buffer, i + 8, WebpIdentifier)) type = "webp";
                        else if (Match(buffer, i + 8, AviSignature)) type = "avi";
                        else if (Match(buffer, i + 8, WavSignature)) type = "wav";
                    }
                    else if (i >= 4 && Match(buffer, i, Mp4Signature)) 
                    {
                        type = "mp4";
                        actualOffset = i - 4; // adjust offset for ftyp
                    }

                    if (type != null)
                    {
                        string extractedName;
                        if (IsValidInMemory(buffer, actualOffset, type, out extractedName))
                        {
                            long fileOffset = baseOffset + actualOffset;
                            string finalName = !string.IsNullOrEmpty(extractedName) ? extractedName : "unknown";
                            if (type == "zip" && finalName.EndsWith(".xml") && finalName.Contains("word")) type = "docx";
                            if (type == "zip" && finalName.EndsWith(".xml") && finalName.Contains("xl")) type = "xlsx";

                            lock (listLock) {
                                if (knownOffsets.Add(fileOffset)) {
                                    allFoundFiles.Add(new FoundFile { Offset = fileOffset, Type = type, Name = finalName, Size = "N/A" });
                                    knownTypes.Add(type);
                                }
                            }
                            
                            // Prevent overlapping detection for long chunk processing.
                            // Only step forward safely to prevent looping
                            if (type == "mp4") i += 3;
                        }
                    }
                }
            });
        }

        private void ScanDisk(string volumePath, int cacheSizeMB)
        {
            using (SafeFileHandle handle = CreateFile(volumePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    Invoke((MethodInvoker)delegate {
                        statusLabel.Text = "Error: Failed to open raw volume. Are you running as Administrator?";
                        statusLabel.ForeColor = Color.Red;
                        scanBtn.Text = "Scan Drive";
                        _scanning = false;
                    });
                    return;
                }

                using (FileStream diskStream = new FileStream(handle, FileAccess.Read))
                {
                    List<Tuple<long, long>> freeExtents = GetVolumeFreeExtents(handle, string.IsNullOrEmpty(driveCombo.SelectedItem.ToString()) ? "C" : driveCombo.SelectedItem.ToString());

                    int bufferSize = cacheSizeMB * 1024 * 1024; 
                    byte[] buffer1 = new byte[bufferSize];
                    byte[] buffer2 = new byte[bufferSize];
                    byte[] readBuffer = buffer1;
                    byte[] processBuffer = null;
                    
                    Task processTask = Task.CompletedTask;

                    long totalProcessed = 0;

                    foreach (var extent in freeExtents)
                    {
                        if (!_scanning) break;
                        long currentOffset = extent.Item1;
                        long extentEnd = extent.Item2;
                        
                        diskStream.Position = currentOffset;

                        while (_scanning && currentOffset < extentEnd)
                        {
                            int toRead = (int)Math.Min(bufferSize, extentEnd - currentOffset);
                            int bytesRead = diskStream.Read(readBuffer, 0, toRead);
                            if (bytesRead <= 0) break;

                            processTask.Wait();
                            
                            processBuffer = readBuffer;
                            readBuffer = (readBuffer == buffer1) ? buffer2 : buffer1;
                            
                            int processBytes = bytesRead;
                            long processOffset = currentOffset;
                            
                            processTask = Task.Run(() => {
                                ProcessBuffer(processBuffer, processBytes, processOffset);
                            });

                            currentOffset += bytesRead;
                            totalProcessed += bytesRead;

                            if (totalProcessed % (200 * 1024 * 1024) == 0)
                            {
                                Invoke((MethodInvoker)delegate {
                                    statusLabel.Text = "Scanning Unallocated Space... " + (totalProcessed / (1024 * 1024)) + " MB scanned.";
                                });
                            }
                        }
                    }
                    if (processTask != null) processTask.Wait();
                }
            }

            Invoke((MethodInvoker)delegate {
                scanBtn.Text = "Scan Drive";
                uiUpdateTimer.Stop();
                lock (listLock) { UpdateListView(); }
                if(statusLabel.Text.StartsWith("Scanning")) statusLabel.Text = "Scan complete. Found: " + allFoundFiles.Count;
                _scanning = false;
            });
        }

        static bool Match(byte[] buffer, int offset, byte[] signature)
        {
            for (int i = 0; i < signature.Length; i++)
                if (buffer[offset + i] != signature[i]) return false;
            return true;
        }

        private bool IsValidInMemory(byte[] buffer, int offset, string type, out string extractedName)
        {
            extractedName = "";
            if (type == "zip") {
                try {
                    // Try to parse ZIP local file header to extract filename
                    if (offset + 30 < buffer.Length) {
                        int nameLen = buffer[offset + 26] | (buffer[offset + 27] << 8);
                        if (nameLen > 0 && nameLen < 256 && offset + 30 + nameLen < buffer.Length) {
                            extractedName = System.Text.Encoding.ASCII.GetString(buffer, offset + 30, nameLen);
                        }
                    }
                } catch { }
                return true; 
            }
            if (type == "gz") {
                 try {
                     // Try to parse GZIP header for original filename (flag bit 3)
                     if (offset + 10 < buffer.Length && (buffer[offset + 3] & 0x08) != 0) {
                         int nameEnd = offset + 10;
                         while (nameEnd < buffer.Length && buffer[nameEnd] != 0 && nameEnd - offset < 256) nameEnd++;
                         if (nameEnd > offset + 10) {
                             extractedName = System.Text.Encoding.ASCII.GetString(buffer, offset + 10, nameEnd - (offset + 10));
                         }
                     }
                 } catch { }
                 return true;
            }

             if (type == "exe") {
                 if (offset + 64 > buffer.Length) return false;
                 int peOffset = BitConverter.ToInt32(buffer, offset + 60);
                 if (peOffset <= 0 || peOffset > 10 * 1024 * 1024 || offset + peOffset + 4 > buffer.Length) return false;
                 if (buffer[offset + peOffset] != 0x50 || buffer[offset + peOffset + 1] != 0x45 || 
                     buffer[offset + peOffset + 2] != 0x00 || buffer[offset + peOffset + 3] != 0x00) return false;
             }
             if (type == "bmp") {
                 if (offset + 14 > buffer.Length) return false;
                 if (buffer[offset + 6] != 0 || buffer[offset + 7] != 0 || buffer[offset + 8] != 0 || buffer[offset + 9] != 0) return false;
             }
             if (type == "mp3") {
                 if (offset + 10 > buffer.Length) return false;
                 if (buffer[offset + 3] > 0x04 || buffer[offset + 3] < 0x02 || buffer[offset + 4] != 0x00) return false;
             }

            if (type != "jpg" && type != "png") return true; // Accept other types with no deep validation

            try {
                int length = Math.Min(buffer.Length - offset, 5 * 1024 * 1024);
                if (length < 100) return false;
                
                byte[] chunk = new byte[length];
                Array.Copy(buffer, offset, chunk, 0, length);
                
                chunk = TrimToEOF(chunk, type);
                
                using (MemoryStream ms = new MemoryStream(chunk)) {
                    using (Image img = Image.FromStream(ms)) {
                        return true;
                    }
                }
            } catch {
                return false;
            }
        }

        private byte[] ReadCarvedBytes(long exactOffset, int length)
        {
            if (driveCombo.SelectedItem == null) return null;
            string volumePath = "\\\\.\\" + driveCombo.SelectedItem.ToString() + ":";

            long sectorSize = 4096;
            long alignedOffset = exactOffset - (exactOffset % sectorSize);
            int diff = (int)(exactOffset - alignedOffset);
            int readLength = length + diff;
            
            if (readLength % sectorSize != 0)
                readLength += (int)(sectorSize - (readLength % sectorSize));

            using (SafeFileHandle handle = CreateFile(volumePath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid) return null;
                using (FileStream diskStream = new FileStream(handle, FileAccess.Read))
                {
                    diskStream.Position = alignedOffset;
                    byte[] buffer = new byte[readLength];
                    int read = diskStream.Read(buffer, 0, readLength);
                    if (read > diff)
                    {
                        int actualDataLength = Math.Min(length, read - diff);
                        byte[] result = new byte[actualDataLength];
                        Array.Copy(buffer, diff, result, 0, actualDataLength);
                        return result;
                    }
                }
            }
            return null;
        }

        private byte[] TrimToEOF(byte[] data, string type)
        {
            if (type == "webp")
            {
                if (data.Length >= 8) {
                    // RIFF format stores exact file size (minus first 8 bytes) in bytes 4-7. WebP is incredibly clean to carve because of this!
                    int size = BitConverter.ToInt32(data, 4) + 8;
                    if (size > 0 && size <= data.Length) {
                        byte[] trimmed = new byte[size];
                        Array.Copy(data, 0, trimmed, 0, size);
                        return trimmed;
                    }
                }
            }
            if (type == "png")
            {
                byte[] iend = { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
                for (int i = 0; i < data.Length - iend.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < iend.Length; j++) if (data[i+j] != iend[j]) { match = false; break; }
                    if (match) {
                        byte[] trimmed = new byte[i + iend.Length];
                        Array.Copy(data, 0, trimmed, 0, trimmed.Length);
                        return trimmed;
                    }
                }
            }
            else if (type == "jpg")
            {
                for(int i = 0; i < data.Length - 1; i++)
                {
                    if (data[i] == 0xFF && data[i+1] == 0xD9)
                    {
                        byte[] trimmed = new byte[i + 2];
                        Array.Copy(data, 0, trimmed, 0, i + 2);
                        return trimmed; 
                    }
                }
            }
            else if (type == "pdf")
            {
                byte[] eof = { 0x25, 0x25, 0x45, 0x4F, 0x46 }; // %%EOF
                byte[] pdfHeader = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
                int bestEof = -1;
                for (int i = 0; i <= data.Length - 5; i++)
                {
                    if (i > 0 && data[i] == pdfHeader[0] && data[i+1] == pdfHeader[1] && data[i+2] == pdfHeader[2] && data[i+3] == pdfHeader[3])
                    {
                        break;
                    }
                    if (data[i] == eof[0] && data[i+1] == eof[1] && data[i+2] == eof[2] && data[i+3] == eof[3] && data[i+4] == eof[4])
                    {
                        bestEof = i + 5;
                    }
                }
                if (bestEof != -1)
                {
                    while (bestEof < data.Length && (data[bestEof] == '\r' || data[bestEof] == '\n'))
                    {
                        bestEof++;
                    }
                    byte[] trimmed = new byte[bestEof];
                    Array.Copy(data, 0, trimmed, 0, bestEof);
                    return trimmed;
                }
            }
            return data;
        }

        private void ResultsListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (resultsListView.SelectedItems.Count == 0) return;
            FoundFile file = resultsListView.SelectedItems[0].Tag as FoundFile;
            if (file != null)
            {
                string baseType = file.Type;
                if (baseType == "webp" || baseType == "zip" || baseType == "docx" || baseType == "xlsx" || baseType == "rar" || baseType == "mp3" || baseType == "exe" || baseType == "sqlite" || baseType == "7z" || baseType == "gz" || baseType == "pdf")
                {
                    Image old = previewBox.Image;
                    previewBox.Image = CreatePlaceholderMessage(file.Type.ToUpper() + " Found at " + file.Offset + "\n\nCannot visually draw this format here natively.\n\nIf you click 'Restore', it WILL save the file.");
                    if (old != null) old.Dispose();
                    return;
                }

                byte[] previewData = ReadCarvedBytes(file.Offset, 5 * 1024 * 1024); 
                if(previewData == null) {
                    previewBox.Image = null;
                    return;
                }

                if (file.Type == "jpg" || file.Type == "png")
                {
                    previewData = TrimToEOF(previewData, file.Type);
                    try {
                        MemoryStream ms = new MemoryStream(previewData); 
                        Image rawImg = Image.FromStream(ms);
                        Image old = previewBox.Image;
                        previewBox.Image = rawImg;
                        if (old != null) old.Dispose(); 
                    } catch {
                        previewBox.Image = CreatePlaceholderMessage("Format chunk decoded, but file body is broken/fragmented\nby Windows over the weeks."); 
                    }
                }
                else {
                    previewBox.Image = null; 
                }
            }
        }

        private void RestoreBtn_Click(object sender, EventArgs e)
        {
            if (resultsListView.SelectedItems.Count == 0) return;
            FoundFile file = resultsListView.SelectedItems[0].Tag as FoundFile;
            if (file != null)
            {
                string outputDir = Path.Combine(Environment.CurrentDirectory, "RecoveredFiles");
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                string baseType = file.Type;
                string namePart = "recovered_" + file.Offset;
                
                if (file.Name != "unknown" && file.Name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) {
                    namePart = file.Name;
                }

                if (string.IsNullOrEmpty(Path.GetExtension(namePart))) {
                     namePart += "." + baseType;
                }

                string outPath = Path.Combine(outputDir, namePart);
                
                byte[] fileData = ReadCarvedBytes(file.Offset, 15 * 1024 * 1024); 
                
                if (fileData != null) {
                    fileData = TrimToEOF(fileData, baseType); 
                    File.WriteAllBytes(outPath, fileData);
                    MessageBox.Show("Successfully recovered the clean file to:\n\n" + outPath, "Restored Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);
                } else {
                }
            }
        }
        
        private void InitializeCleanerTab()
        {
            cleanerDriveCombo = new ComboBox { Location = new Point(10, 10), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach(var di in DriveInfo.GetDrives()) {
                cleanerDriveCombo.Items.Add(di.Name.Substring(0, 1));
            }
            if (cleanerDriveCombo.Items.Count > 0) cleanerDriveCombo.SelectedIndex = 0;

            cleanBtn = new Button { Text = "Clean Drive (DANGER)", Location = new Point(120, 10), Width = 150 };
            cleanBtn.Click += CleanBtn_Click;

            Label cleanCacheLabel = new Label { Location = new Point(280, 15), Width = 70, Text = "Cache (MB):" };
            cleanCacheNumeric = new NumericUpDown { Location = new Point(350, 10), Width = 60, Minimum = 1, Maximum = 1024, Value = 8 };

            cleanerStatusLabel = new Label { Location = new Point(10, 40), Width = 700, Text = "Ready. (Overwrites all found file headers and footers with random data)" };

            cleanerTab.Controls.Add(cleanerDriveCombo);
            cleanerTab.Controls.Add(cleanBtn);
            cleanerTab.Controls.Add(cleanCacheLabel);
            cleanerTab.Controls.Add(cleanCacheNumeric);
            cleanerTab.Controls.Add(cleanerStatusLabel);
        }

        private void CleanBtn_Click(object sender, EventArgs e)
        {
            if (_cleaning) {
                _cleaning = false;
                cleanBtn.Text = "Stopping...";
                return;
            }

            if (cleanerDriveCombo.SelectedItem == null) return;
            string drive = cleanerDriveCombo.SelectedItem.ToString();
            string volumePath = "\\\\.\\" + drive + ":";

            _cleanedCount = 0;
            _cleaning = true;
            cleanBtn.Text = "Stop Cleaning";
            cleanerStatusLabel.Text = "Cleaning " + volumePath + "...";
            cleanerStatusLabel.ForeColor = Color.Red;

            int cacheSizeMB = (int)cleanCacheNumeric.Value;

            ThreadPool.QueueUserWorkItem((state) => {
                CleanDisk(volumePath, cacheSizeMB);
            });
        }

        private int ProcessCleanBuffer(byte[] buffer, int length, long baseOffset)
        {
            int numThreads = Environment.ProcessorCount;
            if (numThreads < 1) numThreads = 1;
            int chunkSize = length / numThreads;

            List<KeyValuePair<long, int>> patches = new List<KeyValuePair<long, int>>();
            object patchLock = new object();

            Parallel.For(0, numThreads, t =>
            {
                int start = t * chunkSize;
                int end = (t == numThreads - 1) ? length - 12 : start + chunkSize;

                for (int i = start; i < end; i++)
                {
                    if (!_cleaning) break;

                    int sigLength = 0;
                    if (Match(buffer, i, JpegSignature)) sigLength = JpegSignature.Length;
                    else if (Match(buffer, i, ZipSignature)) sigLength = ZipSignature.Length;
                    else if (Match(buffer, i, PdfSignature)) sigLength = PdfSignature.Length;
                    else if (Match(buffer, i, PngSignature)) sigLength = PngSignature.Length;
                    else if (Match(buffer, i, ExeSignature)) sigLength = ExeSignature.Length;
                    else if (Match(buffer, i, RarSignature)) sigLength = RarSignature.Length;
                    else if (Match(buffer, i, Mp3Signature)) sigLength = Mp3Signature.Length;
                    else if (Match(buffer, i, RtfSignature)) sigLength = RtfSignature.Length;
                    else if (Match(buffer, i, GifSignature)) sigLength = GifSignature.Length;
                    else if (Match(buffer, i, SqliteSignature)) sigLength = SqliteSignature.Length;
                    else if (Match(buffer, i, GzipSignature)) sigLength = GzipSignature.Length;
                    else if (Match(buffer, i, SevenZipSignature)) sigLength = SevenZipSignature.Length;
                    else if (Match(buffer, i, ElfSignature)) sigLength = ElfSignature.Length;
                    else if (Match(buffer, i, PsdSignature)) sigLength = PsdSignature.Length;
                    else if (Match(buffer, i, OggSignature)) sigLength = OggSignature.Length;
                    else if (Match(buffer, i, FlacSignature)) sigLength = FlacSignature.Length;
                    else if (Match(buffer, i, ClassSignature)) sigLength = ClassSignature.Length;
                    else if (Match(buffer, i, RiffSignature)) {
                        if (Match(buffer, i + 8, WebpIdentifier)) sigLength = 12;
                        else if (Match(buffer, i + 8, AviSignature)) sigLength = 12;
                        else if (Match(buffer, i + 8, WavSignature)) sigLength = 12;
                    }
                    else if (i >= 4 && Match(buffer, i, Mp4Signature)) sigLength = 8;
                    else if (Match(buffer, i, PngEof)) sigLength = PngEof.Length;
                    else if (Match(buffer, i, PdfEof)) sigLength = PdfEof.Length;

                    if (sigLength > 0)
                    {
                        lock(patchLock) {
                            patches.Add(new KeyValuePair<long, int>(baseOffset + (i >= 4 && sigLength == 8 && Match(buffer, i, Mp4Signature) ? i - 4 : i), sigLength));
                        }
                    }
                }
            });

            if (patches.Count > 0)
            {
                Random r = new Random();
                patches.Sort((a, b) => a.Key.CompareTo(b.Key));
                foreach (var p in patches)
                {
                    int localOffset = (int)(p.Key - baseOffset);
                    byte[] randBytes = new byte[p.Value];
                    r.NextBytes(randBytes);
                    if (localOffset >= 0 && localOffset + randBytes.Length <= length)
                    {
                        Array.Copy(randBytes, 0, buffer, localOffset, randBytes.Length);
                    }
                    _cleanedCount++;
                }
            }
            return patches.Count;
        }

        private void CleanDisk(string volumePath, int cacheSizeMB)
        {
            using (SafeFileHandle handle = CreateFile(volumePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    Invoke((MethodInvoker)delegate {
                        cleanerStatusLabel.Text = "Error: Failed to open raw volume for Write. Are you running as Administrator?";
                        cleanBtn.Text = "Clean Drive (DANGER)";
                        _cleaning = false;
                    });
                    return;
                }
                List<Tuple<long, long>> freeExtents = GetVolumeFreeExtents(handle, string.IsNullOrEmpty(cleanerDriveCombo.SelectedItem.ToString()) ? "C" : cleanerDriveCombo.SelectedItem.ToString());

                int bReturned;
                bool isLocked = DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out bReturned, IntPtr.Zero);
                if (!isLocked)
                {
                    Invoke((MethodInvoker)delegate {
                        cleanerStatusLabel.Text = "Error: Cannot lock drive. Windows denies raw writes to active OS drives (C:). Use a secondary drive or boot via USB PE.";
                        cleanBtn.Text = "Clean Drive (DANGER)";
                        _cleaning = false;
                    });
                    return;
                }

                using (FileStream diskStream = new FileStream(handle, FileAccess.ReadWrite))
                {
                        int bufferSize = cacheSizeMB * 1024 * 1024; 
                        byte[] buffer1 = new byte[bufferSize];
                        byte[] buffer2 = new byte[bufferSize];
                        byte[] readBuffer = buffer1;
                        byte[] processBuffer = null;
                        
                        Task processTask = Task.CompletedTask;

                        long totalProcessed = 0;

                        foreach (var extent in freeExtents)
                        {
                            if (!_cleaning) break;
                            long currentOffset = extent.Item1;
                            long extentEnd = extent.Item2;
                            
                            while (_cleaning && currentOffset < extentEnd)
                            {
                                int toRead = (int)Math.Min(bufferSize, extentEnd - currentOffset);
                                int bytesRead;
                                lock (diskStream)
                                {
                                    diskStream.Position = currentOffset;
                                    bytesRead = diskStream.Read(readBuffer, 0, toRead);
                                }
                                if (bytesRead <= 0) break;

                                processTask.Wait();
                                processBuffer = readBuffer;
                                readBuffer = (readBuffer == buffer1) ? buffer2 : buffer1;

                                int processBytes = bytesRead;
                                long processOffset = currentOffset;

                                processTask = Task.Run(() => {
                                    int patchedCount = ProcessCleanBuffer(processBuffer, processBytes, processOffset);
                                    
                                    if (patchedCount > 0)
                                    {
                                        try {
                                            lock (diskStream)
                                            {
                                                diskStream.Position = processOffset;
                                                diskStream.Write(processBuffer, 0, processBytes);
                                            }
                                        } catch (UnauthorizedAccessException) {
                                            Invoke((MethodInvoker)delegate {
                                                cleanerStatusLabel.Text = "Access Denied by OS Volume Manager. Raw write blocked on this partition.";
                                                _cleaning = false;
                                            });
                                        }
                                    }
                                });

                                currentOffset += bytesRead;
                                totalProcessed += bytesRead;

                                if (totalProcessed % (200 * 1024 * 1024) == 0)
                                {
                                    Invoke((MethodInvoker)delegate {
                                        cleanerStatusLabel.Text = "Cleaning Unallocated Space... " + (totalProcessed / (1024 * 1024)) + " MB processed. Overwrites: " + _cleanedCount;
                                    });
                                }
                            }
                        }
                        if (processTask != null) processTask.Wait();
                    }
            }

            Invoke((MethodInvoker)delegate {
                cleanBtn.Text = "Clean Drive (DANGER)";
                cleanerStatusLabel.Text = "Clean complete. Final overwrites: " + _cleanedCount;
                cleanerStatusLabel.ForeColor = Color.Green;
                _cleaning = false;
            });
        }
        
        private List<Tuple<long, long>> GetVolumeFreeExtents(SafeFileHandle handle, string driveLetter)
        {
            List<Tuple<long, long>> freeExtents = new List<Tuple<long, long>>();
            
            uint spc, bps, nfc, tnc;
            if (!GetDiskFreeSpace(driveLetter + ":\\", out spc, out bps, out nfc, out tnc)) return freeExtents;
            
            long clusterSize = (long)spc * bps;

            long startingLcn = 0;
            int outBufferSize = 32 * 1024 * 1024; 
            IntPtr outBuffer = Marshal.AllocHGlobal(outBufferSize);
            try {
                int bytesReturned;
                bool result = DeviceIoControl(handle, FSCTL_GET_VOLUME_BITMAP, ref startingLcn, sizeof(long), outBuffer, outBufferSize, out bytesReturned, IntPtr.Zero);
                
                if (result || Marshal.GetLastWin32Error() == 234) 
                {
                    long returnedStartingLcn = Marshal.ReadInt64(outBuffer, 0);
                    int bytesToRead = bytesReturned - 16;
                    
                    long currentFreeLcnStart = -1;
                    long currentLcn = returnedStartingLcn;
                    
                    for (int byteIdx = 0; byteIdx < bytesToRead && currentLcn < tnc; byteIdx++)
                    {
                        byte b = Marshal.ReadByte(outBuffer, 16 + byteIdx);
                        for (int bit = 0; bit < 8 && currentLcn < tnc; bit++)
                        {
                            bool isAllocated = (b & (1 << bit)) != 0;
                            if (!isAllocated) {
                                if (currentFreeLcnStart == -1) currentFreeLcnStart = currentLcn;
                            } else {
                                if (currentFreeLcnStart != -1) {
                                    freeExtents.Add(new Tuple<long, long>(currentFreeLcnStart * clusterSize, currentLcn * clusterSize));
                                    currentFreeLcnStart = -1;
                                }
                            }
                            currentLcn++;
                        }
                    }
                    if (currentFreeLcnStart != -1) {
                        freeExtents.Add(new Tuple<long, long>(currentFreeLcnStart * clusterSize, currentLcn * clusterSize));
                    }
                }
            } finally {
                Marshal.FreeHGlobal(outBuffer);
            }
            if (freeExtents.Count == 0) freeExtents.Add(new Tuple<long, long>(0, tnc * clusterSize));
            return freeExtents;
        }
    }

    static class Program {
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
