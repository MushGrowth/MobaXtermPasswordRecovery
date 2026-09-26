using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MobaXtermPasswordRecovery.Utils;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;

namespace MobaXtermPasswordRecovery.Softwares
{
    public class MobaXterm
    {
        private static (string UserPrincipalName, string MasterPassword) Sesspass = ("", "");
        private static string SessionP = "";
        private static string IniPath = "";
        private static int Installed = 0;

        // DPAPI 数据格式固定前缀字节数组
        private static readonly byte[] DpapiHeader =
        {
            0x01,
            0x00,
            0x00,
            0x00,
            0xd0,
            0x8c,
            0x9d,
            0xdf,
            0x01,
            0x15,
            0xd1,
            0x11,
            0x8c,
            0x7a,
            0x00,
            0xc0,
            0x4f,
            0xc2,
            0x97,
            0xeb
        };

        public string decryptWithoutMasterPassword(string ciphertext)
        {
            // Extend sessionP to at least 20 characters
            StringBuilder sessionPBuilder = new StringBuilder(SessionP);
            if (sessionPBuilder.Length == 0)
            {
                throw new InvalidDataException("配置中缺少 SessionP，无法解密旧格式密码。");
            }
            while (sessionPBuilder.Length < 20)
            {
                sessionPBuilder.Append(sessionPBuilder);
            }
            string normalizedSessionP = sessionPBuilder.ToString().Substring(0, 20);

            // Construct s2 using Environment variables
            string s2 = (Environment.UserName + Environment.UserDomainName)
                .PadRight(20, ' ')
                .Substring(0, 20);

            // Create key space array with both upper and lower cases
            string[] keySpace = { normalizedSessionP.ToUpper(), normalizedSessionP.ToLower() };

            // Initialize the base key
            byte[] key = Encoding.UTF8.GetBytes("0d5e9n1348/U2+67");
            string validCharacters =
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";

            for (int i = 0; i < key.Length; i++)
            {
                char potentialKeyChar = keySpace[(i + 1) % keySpace.Length][i % 20];
                if (
                    !key.Contains((byte)potentialKeyChar)
                    && validCharacters.Contains(potentialKeyChar)
                )
                {
                    key[i] = (byte)potentialKeyChar;
                }
            }

            HashSet<byte> keySet = new HashSet<byte>(key);
            List<byte> filteredText = new List<byte>();

            foreach (byte t in Encoding.ASCII.GetBytes(ciphertext))
            {
                if (keySet.Contains(t))
                {
                    filteredText.Add(t);
                }
            }

            byte[] ct = filteredText.ToArray();
            List<byte> ptArray = new List<byte>();

            if (ct.Length % 2 == 0)
            {
                for (int i = 0; i < ct.Length; i += 2)
                {
                    int l = Array.IndexOf(key, ct[i]);
                    key = RotateRightBytes(key);
                    int h = Array.IndexOf(key, ct[i + 1]);
                    key = RotateRightBytes(key);
                    ptArray.Add((byte)(16 * h + l));
                }

                return Encoding.UTF8.GetString(ptArray.ToArray());
            }

            return string.Empty;

            // Rotate the key bytes to the right by one position
            byte[] RotateRightBytes(byte[] input)
            {
                byte[] rotatedBytes = new byte[input.Length];
                Array.Copy(input, 0, rotatedBytes, 1, input.Length - 1);
                rotatedBytes[0] = input[input.Length - 1];
                return rotatedBytes;
            }
        }

        private string decryptWithMasterPassword(string ciphertext)
        {
            // 将 base64 字符串解码转换为字节数组
            byte[] MasterPasswordBytes = DecodeBase64(
                Sesspass.MasterPassword,
                "当前 Windows 用户对应的 Master Password 数据"
            );

            // 将 DPAPI 前缀和 MasterPassword 拼接出完整的加密数据
            byte[] fullEncryptedData = new byte[DpapiHeader.Length + MasterPasswordBytes.Length];
            Buffer.BlockCopy(DpapiHeader, 0, fullEncryptedData, 0, DpapiHeader.Length);
            Buffer.BlockCopy(
                MasterPasswordBytes,
                0,
                fullEncryptedData,
                DpapiHeader.Length,
                MasterPasswordBytes.Length
            );

            // 使用 DPAPI 解密。SessionP 为 DPAPI 加解密的 Entropy。
            byte[] temp;
            try
            {
                temp = ProtectedData.Unprotect(
                    fullEncryptedData,
                    Encoding.UTF8.GetBytes(SessionP),
                    DataProtectionScope.CurrentUser
                );
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException(
                    "无法用当前 Windows 用户解锁 Master Password。请在保存密码时使用的原电脑、原 Windows 用户下运行。",
                    exception
                );
            }

            // 将解密后的字节数组转换为字符串（现在获取到的这个字符串是 base64 编码过的）
            string temp2 = Encoding.UTF8.GetString(temp);

            // 将解密后的 base64 字符串解码转换为字节数组
            byte[] output = DecodeBase64(temp2, "DPAPI 解密后的 Master Password 数据");
            if (output.Length < 32)
            {
                throw new InvalidDataException("Master Password 数据长度不足，无法提取 AES 密钥。");
            }

            // 提取 AES 密钥。
            byte[] aeskey = new byte[32];
            Array.Copy(output, aeskey, 32);

            return DecryptMasterCiphertext(ciphertext, aeskey);
        }

        internal static string DecryptMasterCiphertext(string ciphertext, byte[] aeskey)
        {
            byte[] iv;
            if (ciphertext.StartsWith("_@", StringComparison.Ordinal))
            {
                // 26.4: marker + 18 ASCII random characters + Base64 ciphertext.
                // Rijndael uses the first 16 characters of that field as its IV.
                const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";
                if (ciphertext.Length <= 20 || ciphertext.Substring(2, 18).Any(c => !alphabet.Contains(c)))
                    throw new InvalidDataException("新版密码记录头损坏或长度不足。");
                iv = Encoding.ASCII.GetBytes(ciphertext.Substring(2, 16));
                ciphertext = ciphertext.Substring(20);
            }
            else
            {
                iv = AES.Encrypt(new byte[16], aeskey).Take(16).ToArray();
            }

            // AES 解密，获取到明文密码。
            byte[] cipherBytes = DecodeBase64(ciphertext, "连接密码密文");
            string plaintext = AES.Decrypt(cipherBytes, aeskey, iv);
            return plaintext;
        }

        private static byte[] DecodeBase64(string value, string description)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"{description}为空。");
            }

            try
            {
                return Convert.FromBase64String(value.Trim());
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException($"{description}不是有效的 Base64 格式。", exception);
            }
        }

        private static bool IsValidBase64(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                Convert.FromBase64String(value.Trim());
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public void Run(string[] args)
        {
            if (!Initialize(args))
            {
                return;
            }

            // 不存在配置文件，就从注册表里获取信息。即使卸载了，可能还在注册表中保有密码。
            if (!File.Exists(IniPath))
            {
                // Output Credentials
                RegistryKey C = Registry.CurrentUser.OpenSubKey(@"Software\Mobatek\MobaXterm\C");
                if (C != null && C.GetValueNames().Length > 0)
                {
                    Logger.Info("MobaXterm Credentials: ");
                    foreach (string valueName in C.GetValueNames())
                    {
                        string name = valueName;
                        string[] temp = C.GetValue(valueName)
                            .ToString()
                            .Split(new char[] { ':' }, 2);

                        // string[] temp = C.GetValue(valueName).ToString().Split([':'], 2);
                        string userName = temp[0];
                        string ciphertext = temp[1];
                        output(name, userName, ciphertext);
                    }
                }

                // Output Passwords
                RegistryKey P = Registry.CurrentUser.OpenSubKey(@"Software\Mobatek\MobaXterm\P");
                if (P != null && P.GetValueNames().Length > 0)
                {
                    Logger.Info("MobaXterm Passwords: ");
                    foreach (string valueName in P.GetValueNames())
                    {
                        string connName = valueName;
                        string ciphertext = P.GetValue(valueName).ToString();
                        output(null, connName, ciphertext, false);
                    }
                }
            }
            else
            {
                // 从 ini 配置文件加载数据
                IniParserConfiguration parserConfiguration = new IniParserConfiguration
                {
                    AllowDuplicateKeys = true,
                    OverrideDuplicateKeys = true,
                };
                IniDataParser iniDataParser = new IniDataParser(parserConfiguration);
                IniData data = new FileIniDataParser(iniDataParser).ReadFile(IniPath);
                SessionP = data["Misc"]["SessionP"];
                // string MPSetAccount = data["Misc"]["MPSetAccount"];
                // string MPSetComputer = data["Misc"]["MPSetComputer"];
                // Sesspass.UserPrincipalName = $"{MPSetAccount}@{MPSetComputer}";
                Sesspass.MasterPassword = data.Sections.ContainsSection("Sesspass")
                    ? data["Sesspass"][Sesspass.UserPrincipalName] ?? string.Empty
                    : string.Empty;
                if (data.Sections.ContainsSection("Credentials"))
                {
                    KeyDataCollection Credentials = data["Credentials"];
                    Logger.Info("MobaXterm Credentials: ");
                    foreach (KeyData keyData in Credentials)
                    {
                        string name = keyData.KeyName;
                        string[] temp = keyData.Value.Split(new char[] { ':' }, 2);

                        // string[] temp = keyData.Value.Split([':'], 2);
                        string userName = temp[0];
                        string ciphertext = temp[1];
                        output(name, userName, ciphertext);
                    }
                }

                if (data.Sections.ContainsSection("Passwords"))
                {
                    KeyDataCollection Passwords = data["Passwords"];
                    Logger.Info("MobaXterm Passwords: ");
                    foreach (KeyData keyData in Passwords)
                    {
                        string connName = keyData.KeyName;
                        string ciphertext = keyData.Value;
                        output(null, connName, ciphertext, false);
                    }
                }
            }
        }

        private void output(
            string name,
            string username,
            string ciphertext,
            bool isCredential = true
        )
        {
            string recordName = isCredential ? name : username;
            try
            {
                bool hasMasterPassword = !string.IsNullOrWhiteSpace(Sesspass.MasterPassword);
                bool isNewFormat = ciphertext.StartsWith("_@", StringComparison.Ordinal);
                if (isNewFormat && !hasMasterPassword)
                {
                    throw new InvalidDataException("新版密码记录需要当前 Windows 用户对应的 Master Password 数据；请在原电脑、原 Windows 用户下运行。");
                }
                if (hasMasterPassword && !isNewFormat && !IsValidBase64(ciphertext))
                {
                    throw new InvalidDataException("Master Password 已启用，但连接密文不是有效 Base64；格式不受支持或数据损坏。");
                }
                string plaintext = hasMasterPassword
                    ? decryptWithMasterPassword(ciphertext)
                    : decryptWithoutMasterPassword(ciphertext);

                if (plaintext.Length == 0 || plaintext.Any(char.IsControl) || plaintext.Contains('\uFFFD'))
                {
                    throw new InvalidDataException("解密结果为空或包含异常字符，无法确认密码正确。");
                }

                if (isCredential)
                {
                    Logger.Info($"Name:     {name}", indent: true, label: "[*]");
                    Logger.Info($"Username: {username}", indent: true, label: "[*]");
                }
                else
                {
                    Logger.Info($"ConnName: {username}", indent: true, label: "[*]");
                }
                Logger.Info($"Password: {plaintext}", indent: true, label: "[*]");
                Logger.Info("", label: "[*]");
            }
            catch (Exception exception)
            {
                Logger.Info(
                    $"跳过无法解密的记录“{recordName}”：{exception.Message}",
                    label: "[-]"
                );
            }
        }

        private bool Initialize(string[] args)
        {
            Sesspass = ("", "");
            SessionP = "";
            IniPath = "";
            Installed = 0;
            Sesspass.UserPrincipalName = $"{Environment.UserName}@{Environment.MachineName}";
            // 如果传递了配置文件路径，则从配置文件中加载信息进行解密。
            if (args != null && args.Length > 0)
            {
                IniPath = args[0];
            }
            else
            {
                // 便携版，尝试从进程信息中查找 MobaXterm.ini 文件。
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        string description = process.MainModule.FileVersionInfo.FileDescription;
                        if (description == "MobaXterm")
                        {
                            Logger.Debug("Searching for MobaXterm process:");
                            Logger.Debug(
                                $"Process Name: {process.ProcessName}, Version: {process.MainModule.FileVersionInfo.ProductVersion}",
                                indent: true
                            );
                            Logger.Debug(
                                $"Process ID: {process.Id}, Path: {process.MainModule.FileName}",
                                indent: true
                            );
                            IniPath = Path.Combine(
                                Path.GetDirectoryName(process.MainModule.FileName),
                                "MobaXterm.ini"
                            );
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }
            }
            // 安装版。如果没有加载到 MobaXterm.ini 文件，就从注册表中查询信息进行解密
            if (!string.IsNullOrWhiteSpace(IniPath) && File.Exists(IniPath))
            {
                Logger.Info($"MobaXterm.ini configuration file path: {IniPath}");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(IniPath) && !File.Exists(IniPath))
                {
                    Logger.Debug(
                        $"MobaXterm.ini configuration file does not exist: {IniPath}",
                        label: "[-]"
                    );
                }
                Logger.Debug(
                    @"Read information from the registry: HKEY_CURRENT_USER\Software\Mobatek\MobaXterm\"
                );

                // 也可以通过注册表项 SessionP 判断主机上是否运行过 MobaXterm，因为不管是安装版本（Installer），还是便携版本（Portable）运行过都会有这个注册表项。
                SessionP = (string)
                    Registry.GetValue(
                        @"HKEY_CURRENT_USER\Software\Mobatek\MobaXterm\",
                        "SessionP",
                        ""
                    );

                // 少数逆天情况，即使注册表中没有 SessionP，也会默认在“用户文档”目录下存在配置文件。
                string tempIniPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"MobaXterm\MobaXterm.ini"
                );

                // 当注册表没有 SessionP，且没有有效的 ini 配置文件的时候，判定电脑中没有该软件。
                if (string.IsNullOrWhiteSpace(SessionP) && !File.Exists(tempIniPath))
                {
                    Logger.Info("MobaXterm does not exist on this machine.", label: "[x]");
                    return false;
                }
                else if (File.Exists(tempIniPath))
                {
                    IniPath = tempIniPath;
                    Logger.Info($"MobaXterm.ini configuration file path: {IniPath}");
                }

                // 通过注册表项 installed 是否存在，来判断主机上的 MobaXterm 是安装版本（Installer），还是便携版本（Portable）。
                object temp_installed = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Mobatek\MobaXterm\",
                    "installed",
                    0
                );

                if (temp_installed != null && temp_installed is int)
                {
                    Installed = (int)temp_installed;
                }

                // Sesspass.UserPrincipalName = $"{Environment.UserName}@{Environment.MachineName}";
                // Master Password
                Sesspass.MasterPassword =
                    (string)
                        Registry.GetValue(
                            @"HKEY_CURRENT_USER\Software\Mobatek\MobaXterm\M",
                            Sesspass.UserPrincipalName,
                            ""
                        ) ?? string.Empty;
            }
            if (Installed.Equals(0))
            {
                Logger.Info("MobaXterm Portable Edition");
            }
            else
            {
                Logger.Info("MobaXterm Installer Edition");
            }
            return true;
        }
    }
}
