using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AddOnAutoLotesInstaller
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int EndInstallDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetAddOnFolderDelegate(string srrPath);

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                string targetDir = @"C:\Program Files\SAP\SAP Business One\AddOns\Lienzo\AddOnAutoLotes";
                string dllPath = "";

                if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                {
                    string raw = args[0];
                    if (raw.Contains("|"))
                    {
                        string[] parts = raw.Split('|');
                        targetDir = parts[0];
                        if (parts.Length > 1) dllPath = parts[1];
                    }
                    else
                    {
                        targetDir = raw;
                    }
                }

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 1. Extraer o copiar AddOnAutoLotes.exe
                string targetExe = Path.Combine(targetDir, "AddOnAutoLotes.exe");
                ExtractResourceOrCopy("AddOnAutoLotes.exe", targetExe);

                // 2. Extraer o copiar AddOnAutoLotes.exe.config
                string targetCfg = Path.Combine(targetDir, "AddOnAutoLotes.exe.config");
                ExtractResourceOrCopy("AddOnAutoLotes.exe.config", targetCfg);

                // 3. Notificar a SAP que la instalación finalizó mediante AddOnInstallAPI
                if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
                {
                    string sapDir = @"C:\Program Files\SAP\SAP Business One";
                    if (File.Exists(Path.Combine(sapDir, "AddOnInstallAPI_x64.dll")))
                        dllPath = Path.Combine(sapDir, "AddOnInstallAPI_x64.dll");
                    else if (File.Exists(Path.Combine(sapDir, "AddOnInstallAPI.dll")))
                        dllPath = Path.Combine(sapDir, "AddOnInstallAPI.dll");
                }

                if (!string.IsNullOrEmpty(dllPath) && File.Exists(dllPath))
                {
                    IntPtr hLib = LoadLibrary(dllPath);
                    if (hLib != IntPtr.Zero)
                    {
                        IntPtr pSetFolder = GetProcAddress(hLib, "SetAddOnFolder");
                        if (pSetFolder != IntPtr.Zero)
                        {
                            var setFolder = (SetAddOnFolderDelegate)Marshal.GetDelegateForFunctionPointer(pSetFolder, typeof(SetAddOnFolderDelegate));
                            setFolder(targetDir);
                        }

                        IntPtr pEnd = GetProcAddress(hLib, "EndInstall");
                        if (pEnd != IntPtr.Zero)
                        {
                            var endInstall = (EndInstallDelegate)Marshal.GetDelegateForFunctionPointer(pEnd, typeof(EndInstallDelegate));
                            endInstall();
                        }

                        FreeLibrary(hLib);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error durante la instalación de AddOnAutoLotes: " + ex.Message, 
                                "Instalador AddOnAutoLotes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ExtractResourceOrCopy(string resourceName, string targetPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (FileStream fs = File.Create(targetPath))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                        }
                    }
                    return;
                }
            }

            // Fallback: copiar desde el directorio actual
            string myDir = Path.GetDirectoryName(asm.Location);
            string localFile = Path.Combine(myDir, resourceName);
            if (!File.Exists(localFile))
            {
                localFile = Path.Combine(myDir, "X64Client", resourceName);
            }
            if (File.Exists(localFile))
            {
                File.Copy(localFile, targetPath, true);
            }
        }
    }
}
