using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RouteForwarder
{
    public partial class Form1 : Form
    {
        private bool IsRoutingEnabled(string gatewayAddress)
        {

            try
            {
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters");
                if (registryKey != null)
                {
                    int value = (int?)Convert.ToInt32(registryKey.GetValue("IPEnableRouter")) ?? -1;
                    if (value != 1)
                    {
                        return false;
                    }
                    uint ifindex;
                    RouteTableManager.GetBestInterface(IPAddress.Parse(gatewayAddress), out ifindex);
                    RouteTableManager.MIB_IPINTERFACE_ROW ro = new RouteTableManager.MIB_IPINTERFACE_ROW();
                    RouteTableManager.GetIpInterfaceEntry(ifindex, out ro);
                    return ro.ForwardingEnabled == 1;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("发生异常：" + ex.Message);
                return false;
            }
        }




        static void SetInterfaceIpv4Status(string interfaceId, bool forwardingEnabled)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"interface ipv4 set interface {interfaceId} forwarding={(forwardingEnabled ? "enabled" : "disabled")}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                process.WaitForExit();
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            GatewayIPAddressInformation defaultGateway = null;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet && !(ni.Description.Contains("TAP-Windows Adapter") || ni.Description.Contains("OpenVPN TAP")))
                {
                    GatewayIPAddressInformationCollection gateways = ni.GetIPProperties().GatewayAddresses;
                    if (gateways.Any())
                    {
                        defaultGateway = gateways.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                        comboBox2.Items.Add(ni.Name);
                    }

                }
            }


            if (defaultGateway != null)
            {
                tb_fm_max.Text = defaultGateway.Address.ToString();
                comboBox2.SelectedIndex = comboBox2.Items.Count - 1;

            }
            else
            {
                MessageBox.Show("获取默认网关失败！请手动选择");
            }

            string extraFilePath = Path.Combine(Application.StartupPath, "extra.txt");
            if (File.Exists(extraFilePath))
            {
                textBox1.Lines = File.ReadAllLines(extraFilePath);
            }

            string[] files = Directory.GetFiles("./list", "*.txt");

            cbList.Items.AddRange(files.Select(Path.GetFileName).ToArray());


            cbList.SelectedIndex = Math.Min(1, cbList.Items.Count - 1);
            cb1.Checked = IsRoutingEnabled(tb_fm_max.Text);


            comboBox1.SelectedIndex = 0;
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.SelectedItem != null)
            {
                NetworkInterface selectedInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni => ni.Name == comboBox2.SelectedItem.ToString());
                if (selectedInterface != null)
                {
                    GatewayIPAddressInformation gateway = selectedInterface.GetIPProperties()
                        .GatewayAddresses.FirstOrDefault();
                    if (gateway != null)
                    {
                        tb_fm_max.Text = gateway.Address.ToString();
                    }
                    else
                    {
                        tb_fm_max.Text = "";
                    }
                }

            }
        }

        private void cb1_Click(object sender, EventArgs e)
        {
            const string keyPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
            const string valueName = "IPEnableRouter";
            int targetValue = 1;
            bool changed = false;
            bool isChecked = cb1.Checked;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true))
                {
                    if (key == null)
                    {
                        using (RegistryKey newKey = Registry.LocalMachine.CreateSubKey(keyPath))
                        {
                            newKey.SetValue(valueName, targetValue, RegistryValueKind.DWord);
                        }
                        changed = true;
                    }
                    else
                    {
                        object value = key.GetValue(valueName);
                        if (value == null || !(value is int) || (int)value != targetValue)
                        {
                            key.SetValue(valueName, targetValue, RegistryValueKind.DWord);
                            changed = true;
                        }
                    }
                }

                uint ifindex;
                RouteTableManager.GetBestInterface(IPAddress.Parse(tb_fm_max.Text), out ifindex);
                if (isChecked)
                {
                    SetInterfaceIpv4Status(ifindex.ToString(), true);
                    if (changed)
                    {
                        DialogResult result1 = MessageBox.Show("本次设置需重启才能生效，是否立即重启计算机？", "提示", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);

                        if (DialogResult.Yes == result1)
                        {
                            Process.Start("shutdown.exe", "-r -t 0");
                        }
                    }

                }
                else
                {
                    SetInterfaceIpv4Status(ifindex.ToString(), false);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("发生异常：" + ex.Message);
                cb1.Checked = !isChecked;
            }
        }

        public static string CdirToSubnetMask(string cdir)
        {
            string[] parts = cdir.Split('/');
            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid cdir format: " + cdir);
            }

            int prefixLength;
            if (!int.TryParse(parts[1], out prefixLength) || prefixLength < 0 || prefixLength > 32)
            {
                throw new ArgumentException("Invalid prefix length: " + parts[1]);
            }

            uint subnetMask = uint.MaxValue << (32 - prefixLength);
            return new IPAddress(BitConverter.GetBytes(subnetMask).Reverse().ToArray()).ToString();
        }
        private async void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;

            if (!IPAddress.TryParse(tb_fm_max.Text.Trim(), out IPAddress defaultGateway))
            {
                MessageBox.Show("请检查默认网关的 IP 地址是否正确。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                button3.Enabled = true;
                return;
            }
            if (cbList.Text == "无" && !textBox1.Enabled)
            {
                MessageBox.Show("没有处理任何路由条目。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Error);
                button3.Enabled = true;
                return;
            }

            try
            {
                uint ifindex;
                RouteTableManager.GetBestInterface(IPAddress.Parse(tb_fm_max.Text), out ifindex);
                string gateway = tb_fm_max.Text;
                IPAddress nexthop = IPAddress.Parse(gateway);
                RouteTableManager.MIB_IPINTERFACE_ROW ro = new RouteTableManager.MIB_IPINTERFACE_ROW();
                RouteTableManager.GetIpInterfaceEntry(ifindex, out ro);

                string[] routeInfo;
                string address;
                string netmask;

                if (textBox1.Enabled)
                {
                    string[] lines = textBox1.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .ToArray();

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (IPAddress.TryParse(line, out IPAddress ip))
                        {
                            if (comboBox1.Text == "添加路由")
                            { RouteTableManager.createIpForwardEntry(RouteTableManager.IPToInt(line.ToString()), RouteTableManager.IPToInt("255.255.255.255"), RouteTableManager.IPToInt(gateway), (UInt32)ifindex, (int)ro.Metric); }
                            else
                            {
                                RouteTableManager.deleteIpForwardEntry(RouteTableManager.IPToInt(line.ToString()), RouteTableManager.IPToInt("255.255.255.255"), RouteTableManager.IPToInt(gateway), ifindex);
                            }
                        }
                        else
                        {
                            try
                            {
                                var addresses = await Dns.GetHostAddressesAsync(line);

                                foreach (var a in addresses)
                                {
                                    if (a.AddressFamily == AddressFamily.InterNetwork)
                                    {
                                        if (comboBox1.Text == "添加路由")
                                        { RouteTableManager.createIpForwardEntry(RouteTableManager.IPToInt(a.ToString()), RouteTableManager.IPToInt("255.255.255.255"), RouteTableManager.IPToInt(gateway), (UInt32)ifindex, (int)ro.Metric); }
                                        else
                                        {
                                            RouteTableManager.deleteIpForwardEntry(RouteTableManager.IPToInt(a.ToString()), RouteTableManager.IPToInt("255.255.255.255"), RouteTableManager.IPToInt(gateway), ifindex);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"无法解析 {line}：{ex.Message}");

                            }

                        }
                    }

                    string text = string.Join(Environment.NewLine, lines);
                    string filePath = Path.Combine(Application.StartupPath, "extra.txt");
                    File.WriteAllText(filePath, text);

                }

                if (cbList.Text != "无")
                {
                    string routeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list", cbList.Text);
                    string[] fileContents = File.ReadAllLines(routeDir, Encoding.Default);

                    if (comboBox1.Text == "添加路由")
                    {
                        for (int i = 0; i < fileContents.Length; i++)
                        {
                            routeInfo = fileContents[i].Split('/');

                            address = routeInfo[0];
                            netmask = CdirToSubnetMask(fileContents[i]);
                            RouteTableManager.createIpForwardEntry(RouteTableManager.IPToInt(address), RouteTableManager.IPToInt(netmask), RouteTableManager.IPToInt(gateway), (UInt32)ifindex, (int)ro.Metric);

                        }
                    }
                    else
                    {

                        for (int i = 0; i < fileContents.Length; i++)
                        {
                            routeInfo = fileContents[i].Split('/');
                            address = routeInfo[0];
                            netmask = CdirToSubnetMask(fileContents[i]);

                            RouteTableManager.deleteIpForwardEntry(RouteTableManager.IPToInt(address), RouteTableManager.IPToInt(netmask), RouteTableManager.IPToInt(gateway), ifindex);

                        }
                    }

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                MessageBox.Show("执行完成！");
                button3.Enabled = true;
            }


        }

        private void checkBox1_Click(object sender, EventArgs e)
        {
            textBox1.Enabled = checkBox1.Checked;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            Process.Start("cmd.exe", "/K route print");
        }

        private void linkLabel5_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://youtube.com/@bulianglin");
        }

        private void tb_fm_max_Leave(object sender, EventArgs e)
        {
            tb_fm_max.Text = tb_fm_max.Text.Trim();
            cb1.Checked = IsRoutingEnabled(tb_fm_max.Text);
        }
    }
}
