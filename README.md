# Data Carver

Data Carver is a high-performance, multithreaded raw disk carving and data destruction utility for Windows. 

By bypassing the Windows operating system APIs and the NTFS Master File Table entirely, this tool physically queries the raw sectors of your hard drive. It can reconstruct deleted, fragmented, or stealthily hidden files from unallocated space based purely on their hex signatures.

Conversely, the "Data Cleaner" module is capable of locking a partition, seeking out orphaned file signatures floating in unallocated space, and permanently shredding them with cryptographic garbage, ensuring no other recovery tools can restore your wiped data.

## Features
- **Zero-Latency Double Buffering**: The disk I/O pipeline perpetually fetches 8-1024MB chunks asynchronously while the CPU aggressively shreds the active cache, slashing processing times.
- **Multithreaded Engine**: Dispatches signature hunting and cryptographic random generation across all logical CPU cores via `Parallel.For`.
- **Targeted Unallocated Space**: Safely bypasses active OS data and `pagefile.sys` structures by dynamically querying `FSCTL_GET_VOLUME_BITMAP` to meticulously trace and operate *only* within free extents.
- **Over 20+ File Formats**: Supports extraction of JPG, PNG, PDF, ZIP, MP4, MP3, SQLITE, WEBP, DOCX/XLSX (via ZIP signatures), and many more.

## Prerequisites
- **Target OS**: Windows 10 / Windows 11
- **Framework**: .NET Framework 4.0 or higher
- **Privileges**: Running the executable strictly requires **Administrator Privileges** (to request low-level `DeviceIoControl` access to raw storage volumes like `\\.\C:`).

---

## Build Instructions (Command Line)

You do not need Visual Studio to compile this application. Microsoft includes the native C# compiler (`csc.exe`) directly inside Windows by default. 

To compile a highly optimized, windowed `.exe` with the custom shield logo natively on your machine:

1. Open PowerShell or Command Prompt.
2. Navigate to the directory containing this source code.
3. Run the following build command:

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /win32icon:logo.ico /out:DataCarver.exe Program.cs
```

### Breaking down the command:
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` - Path to the native C# compiler included with Windows.
- `/target:winexe` - Tells the compiler to generate a GUI application (instead of a console application that spawns a black CMD window in the background).
- `/win32icon:logo.ico` - Compiles the provided standard `logo.ico` file deeply into the PE header of the software so it shows up natively in the Taskbar and Windows Explorer.
- `/out:DataCarver.exe` - The final executable filename.
- `Program.cs` - The main C# source file containing the engine architecture.

## Application Tabs

1. **Raw Disk Carver**
   - **Purpose**: Scans the unallocated (empty) space of a physical drive to reconstruct lost, deleted, or orphaned files. 
   - **Features**: Live parsing of discovered formats (PDF, JPG, ZIP, MP4, etc.), filtering by file type, direct file restoration to a `./RecoveredFiles/` directory, and an isolated UI renderer for natively previewing restored images. 

2. **Test Normal Files**
   - **Purpose**: A diagnostic area intended to help developers determine if file structures recovered by the Carver are actually valid and recognizable by standard Windows Graphics hooks. 
   - **Features**: Users can test previously carved image blobs by directly pointing the interface renderer at them. It helps isolate whether a "broken image" result is corrupted on the disk itself or just unsupported by basic Windows APIs (like `webp`).

3. **Data Cleaner**
   - **Purpose**: Permanently destroys data leaks from deleted files lying dormant out of the operating system's reach. 
   - **Features**: Rather than blinding writing zeros over the entire drive and slowly decreasing the SSD's lifespan, this engine specifically maps out empty space, hunts exclusively for file headers/footers in the raw bytes, and atomically overwrites only the exact signatures with randomized cryptographic data. 

## Usage Guide
1. **Right-Click `DataCarver.exe`** and select **Run as Administrator**.
2. Select your target drive letter from the dropdown box.
3. Adjust the **Cache (MB)** slider. If you have plenty of RAM, raising this to `100+ MB` will vastly accelerate the speed by swallowing huge sections of the disk per iteration.
4. Hit **Scan Drive** to discover floating files, or **Clean Drive** to sanitize unallocated sectors.
5. If scanning, select a file type from the filter and click a row to render a preview of the recovered data (if supported), or highlight it and click **Restore Selected** to perfectly rebuild the document.

> **Note on OS Drives (`C:`)**: 
> Windows strictly protects the primary `C:` drive with its volume manager. While the **Raw Scanner** can easily read your `C:` drive while you are actively using Windows, the **Data Cleaner** tab cannot *overwrite* sectors on `C:` without exclusively locking the partition (`FSCTL_LOCK_VOLUME`). You can clean any external/secondary drives smoothly. To "Clean" the primary `C:` drive using this utility, you must run it from a bootable Windows PE (Preinstallation Environment) USB drive.
