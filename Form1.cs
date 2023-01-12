using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpPcap;
using System.Threading;

// PC devices 0 a 1
// MAC devices 3 a 0

namespace PSIP_Zadanie
{
    public partial class Form1 : Form
    {
        private CaptureDeviceList devices = CaptureDeviceList.Instance;
        private static List<RawCapture> packetQueue1 = new List<RawCapture>();
        private static List<RawCapture> packetQueue2 = new List<RawCapture>();
        private static List<List<Object>> macTable = new List<List<Object>>();
        private static List<List<Object>> filterList = new List<List<Object>>();
        HashSet<byte[]> sentPackets1 = new HashSet<byte[]>(new bytearraycomparer());
        HashSet<byte[]> sentPackets2 = new HashSet<byte[]>(new bytearraycomparer());
        private bool int1connect = false;
        private bool int2connect = false;

        int ETH1i = 0, ETH2i = 0, ETH1o = 0, ETH2o = 0;
        int IP1i = 0, IP2i = 0, IP1o = 0, IP2o = 0;
        int ARP1i = 0, ARP2i = 0, ARP1o = 0, ARP2o = 0;
        int TCP1i = 0, TCP2i = 0, TCP1o = 0, TCP2o = 0;
        int UDP1i = 0, UDP2i = 0, UDP1o = 0, UDP2o = 0;
        int HTTP1i = 0, HTTP2i = 0, HTTP1o = 0, HTTP2o = 0;
        int ICMP1i = 0, ICMP2i = 0, ICMP1o = 0, ICMP2o = 0;
        int timeOut = 60;

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            timeOut = (int)numericUpDown1.Value;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ETH2i = 0; ETH2o = 0; IP2i = 0; IP2o = 0; ARP2i = 0; ARP2o = 0; TCP2i = 0; TCP2o = 0; UDP2i = 0; UDP2o = 0; ICMP2i = 0; ICMP2o = 0;
            updateStatistics();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ETH1i = 0; ETH1o = 0; IP1i = 0; IP1o = 0; ARP1i = 0; ARP1o = 0; TCP1i = 0; TCP1o = 0; UDP1i = 0; UDP1o = 0; ICMP1i = 0; ICMP1o = 0;
            updateStatistics();
        }

        private void radioButton7_Click(object sender, EventArgs e)
        {
            icmpCombo.Enabled = true;
            srcPortText.Enabled = false;
            dstPortText.Enabled = false;
        }

        private void radioButton6_Click(object sender, EventArgs e)
        {
            icmpCombo.Enabled = false;
            srcPortText.Enabled = true;
            dstPortText.Enabled = true;
        }

        private void radioButton5_Click(object sender, EventArgs e)
        {
            icmpCombo.Enabled = false;
            srcPortText.Enabled = true;
            dstPortText.Enabled = true;
        }

        private void radioButton8_Click(object sender, EventArgs e)
        {
            icmpCombo.Enabled = false;
            srcPortText.Enabled = false;
            dstPortText.Enabled = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            macTable.Clear();
            updateMacTable();
        }

        private void newFilter_Click(object sender, EventArgs e)
        {
            addFilter();
        }

        private void deleteFilter_Click(object sender, EventArgs e)
        {
            var selectedItems = filterListView.SelectedItems;
            for(int i = 0; i < selectedItems.Count; i++)
                for(int j = 0; j < filterList.Count; j++)
                    if(int.Parse(selectedItems[i].Text) == (int)filterList[j][0])
                        filterList.RemoveAt(j);
            updateFilterTable();
        }

        public Form1()
        {
            InitializeComponent();
            Thread interface1 = new Thread(Interface1Thread);
            Thread interface2 = new Thread(Interface2Thread);
            Thread interfaceTimer = new Thread(InterfaceTimer);
            interface1.Start();
            interface2.Start();
            interfaceTimer.Start();
        }

        public class bytearraycomparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] a, byte[] b)
            {
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }
            public int GetHashCode(byte[] a)
            {
                uint b = 0;
                for (int i = 0; i < a.Length; i++)
                    b = ((b << 23) | (b >> 9)) ^ a[i];
                return unchecked((int)b);
            }
        }
        public String getSourceMacAddress(byte[] rawPacket)
        {
            string macAddress = "";
            for (int i = 6; i <= 11; i++)
            {
                if (rawPacket[i].ToString().Length > 1)
                    macAddress += rawPacket[i].ToString("X");
                else
                    macAddress += "0" + rawPacket[i].ToString();
                if (i < 11)
                    macAddress += ":";
            }
            return macAddress;
        }
        public String getDestMacAddress(byte[] rawPacket)
        {
            string macAddress = "";
            for (int i = 0; i <= 5; i++)
            {
                if (rawPacket[i].ToString().Length > 1)
                    macAddress += rawPacket[i].ToString("X");
                else
                    macAddress += "0" + rawPacket[i].ToString();
                if (i < 5)
                    macAddress += ":";
            }
            return macAddress;
        }
        private String getSourceIpAddress(byte[] rawPacket)
        {
            string ipAddress = "";
            for(int i = 26; i <= 29; i++)
            {
                ipAddress += rawPacket[i];

                if(i < 29)
                    ipAddress += ".";
            }
            return ipAddress;
        }
        private String getDestIpAddress(byte[] rawPacket)
        {
            string ipAddress = "";
            for(int i = 30; i <= 33; i++){

                ipAddress += rawPacket[i];
                if(i < 33)
                    ipAddress += ".";
            }
            return ipAddress;
        }
        public void addMacRecord(String macAddress, int portNumber)
        {
            List<object> record = new List<object>();
            record.Add(macAddress);
            record.Add(portNumber);
            record.Add(timeOut);
            macTable.Add(record);
        }

        public void refreshMacRecord(String macAddress, int portNumber)
        {
            for (int i = 0; i < macTable.Count; i++)
                if (macTable[i][0].Equals(macAddress))
                {
                    macTable[i][1] = portNumber;
                    macTable[i][2] = timeOut;
                    return;
                }
        }
        public void addFilter()
        {
            List<object> rule = new List<object>();
            if (prioTextbox.Text == "")
                addStatus.Text = "No priority was selected";
            else
            {
                rule.Add(int.Parse(prioTextbox.Text));  //0
                if (permit.Checked)  //1
                    rule.Add(true);
                else
                    rule.Add(false);
                rule.Add(intComboBox.SelectedIndex+1);  //2
                if (radioButton4.Checked)  //3
                    rule.Add("In");
                else
                    rule.Add("Out");
                rule.Add(parseText(srcMACTextbox.Text)); //  4
                rule.Add(parseText(dstMACTextbox.Text)); // 5
                rule.Add(parseText(srcIPTextbox.Text)); // 6
                rule.Add(parseText(dstIPTextbox.Text)); // 7
                if(any.Checked)
                    rule.Add("Any");
                else if (tcp.Checked) // 8
                    rule.Add("TCP");
                else if (udp.Checked)
                    rule.Add("UDP");
                else if (icmp.Checked)
                    rule.Add("ICMP");

                rule.Add(parseText(srcPortText.Text)); // 9
                rule.Add(parseText(dstPortText.Text)); // 10
                rule.Add(parseText(icmpCombo.Text)); // 11
                if (!filterListContains((int)rule[0]))
                {
                    addStatus.Text = "";
                    filterList.Add(rule);
                    filterList = filterList.OrderBy(x => x[0]).ToList();
                    updateFilterTable();
                }
                else
                    addStatus.Text = "Rule with selected priority already exists";
            }
        }
        private bool filterListContains(int priority)
        {
            for(int i = 0; i < filterList.Count; i++)
            {
               if((int)filterList[i][0] == priority)
                    return true;
            }
            return false;
        }
        private String parseText(String text)
        {
            if (text == "")
                return "Any";
            else
                return text;
        }
        private void Device1_OnPacketArrival(object sender, PacketCapture e)
        {
            if (!sentPackets1.Contains(e.Data.ToArray()))
                if (e.Data[6] != 2 && e.Data[7] != 0 && e.Data[8] != 76 && e.Data[9] != 79 && e.Data[10] != 79 && e.Data[11] != 80)
                    if(checkFilter(1,"In",e.GetPacket()))
                {
                    var rawPacket = e.GetPacket();

                    String sourceMac = getSourceMacAddress(rawPacket.Data);
                    String destMac = getDestMacAddress(rawPacket.Data);
                    int sourcePort = findMacRecord(sourceMac);
                    int destPort = findMacRecord(destMac);

                    if (sourcePort == 0)
                        addMacRecord(sourceMac, 1);
                    else
                        refreshMacRecord(sourceMac, 1);
                    updateMacTable();

                    if (destPort == 0 || destMac.Equals("ff:ff:ff:ff:ff")) // Send to all ports
                        packetQueue2.Add(rawPacket);
                    else
                        if (destPort == 2)
                        packetQueue2.Add(rawPacket);
                    //Console.WriteLine("Received packet on port" + " 1 " + "from MAC Address: " + sourceMac);
                    if (!sourceMac.Equals("00:50:79:66:68:03"))
                    {
                        if (rawPacket.LinkLayerType.ToString() == "Ethernet")
                            ETH1i++;
                        if (rawPacket.Data[12] == 8 && rawPacket.Data[13] == 6)
                            ARP1i++;
                        if (rawPacket.Data[12] == 8 && rawPacket.Data[13] == 0)
                        {
                            IP1i++;
                            if (rawPacket.Data[23] == 6)
                            {
                                TCP1i++;
                                if (rawPacket.Data[34] == 0 && rawPacket.Data[35] == 80 || rawPacket.Data[36] == 0 && rawPacket.Data[37] == 80)
                                    HTTP1i++;
                            }
                            if (rawPacket.Data[23] == 17)
                                UDP1i++;
                            if (rawPacket.Data[23] == 1)
                                ICMP1i++;
                        }
                    }

                    updateStatistics();
                }
        }
        private void Device2_OnPacketArrival(object sender, PacketCapture e)
        {
            if (!sentPackets2.Contains(e.Data.ToArray()))
                if (e.Data[6] != 2 && e.Data[7] != 0 && e.Data[8] != 76 && e.Data[9] != 79 && e.Data[10] != 79 && e.Data[11] != 80)
                    if(checkFilter(2,"In",e.GetPacket()))
                {
                    var rawPacket = e.GetPacket();

                    String sourceMac = getSourceMacAddress(rawPacket.Data);
                    String destMac = getDestMacAddress(rawPacket.Data);
                    int sourcePort = findMacRecord(sourceMac);
                    int destPort = findMacRecord(destMac);

                    if (sourcePort == 0)
                        addMacRecord(sourceMac, 2);
                    else
                        refreshMacRecord(sourceMac, 2);
                    updateMacTable();

                    if (destPort == 0 || destMac.Equals("ff:ff:ff:ff:ff")) // Send to all ports
                        packetQueue1.Add(rawPacket);
                    else
                        if (destPort == 1)
                        packetQueue1.Add(rawPacket);
                    if (!sourceMac.Equals("00:50:79:66:68:04"))
                    {
                        if (rawPacket.LinkLayerType.ToString() == "Ethernet")
                            ETH2i++;
                        if (rawPacket.Data[12] == 8 && rawPacket.Data[13] == 6)
                            ARP2i++;
                        if (rawPacket.Data[12] == 8 && rawPacket.Data[13] == 0)
                        {
                            IP2i++;
                            if (rawPacket.Data[23] == 6)
                            {
                                TCP2i++;
                                if (rawPacket.Data[34] == 0 && rawPacket.Data[35] == 80 || rawPacket.Data[36] == 0 && rawPacket.Data[37] == 80)
                                    HTTP2i++;
                            }

                            if (rawPacket.Data[23] == 17)
                                UDP2i++;
                            if (rawPacket.Data[23] == 1)
                                ICMP2i++;
                        }
                    }
                    updateStatistics();
                }
        }
        private void Interface1Thread()
        {
            var device = devices[3];
            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(Device1_OnPacketArrival);
            device.Open(DeviceModes.NoCaptureLocal, read_timeout: 100);
            Console.WriteLine("-- Listening on {0} {1}, hit 'Enter' to stop...", device.Name, device.Description);
            device.StartCapture();

            while (true)
            {
                while (packetQueue1.Count > 0)
                {
                    if (checkFilter(1, "Out", packetQueue1[0]))
                    {
                        device.SendPacket(packetQueue1[0]);
                        sentPackets1.Add(packetQueue1[0].Data);
                        if (!getDestMacAddress(packetQueue1[0].Data).Equals("00:50:79:66:68:03"))
                        {
                            if (packetQueue1[0].LinkLayerType.ToString() == "Ethernet")
                                ETH1o++;
                            if (packetQueue1[0].Data[12] == 8 && packetQueue1[0].Data[13] == 6)
                                ARP1o++;
                            if (packetQueue1[0].Data[12] == 8 && packetQueue1[0].Data[13] == 0)
                            {
                                IP1o++;
                                if (packetQueue1[0].Data[23] == 6)
                                {
                                    TCP1o++;
                                    if (packetQueue1[0].Data[34] == 0 && packetQueue1[0].Data[35] == 80 || packetQueue1[0].Data[36] == 0 && packetQueue1[0].Data[37] == 80)
                                        HTTP1o++;
                                }
                                if (packetQueue1[0].Data[23] == 17)
                                    UDP1o++;
                                if (packetQueue1[0].Data[23] == 1)
                                    ICMP1o++;
                            }
                        }
                    }                    packetQueue1.RemoveAt(0);
                    updateStatistics();
                }
                Thread.Sleep(10);
            }

            //device.StopCapture();
            //device.Close();
        }

        private void Interface2Thread()
        {
            var device = devices[4];

            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(Device2_OnPacketArrival);
            device.Open(mode: DeviceModes.Promiscuous | DeviceModes.NoCaptureLocal, read_timeout: 100);
            Console.WriteLine("-- Listening on {0} {1}, hit 'Enter' to stop...", device.Name, device.Description);
            device.StartCapture();

            while (true)
            {
                while (packetQueue2.Count > 0)
                {
                    if (checkFilter(2, "Out", packetQueue2[0]))
                    {
                        device.SendPacket(packetQueue2[0]);
                        sentPackets2.Add(packetQueue2[0].Data);
                        if (!getDestMacAddress(packetQueue2[0].Data).Equals("00:50:79:66:68:04"))
                        {
                            if (packetQueue2[0].LinkLayerType.ToString() == "Ethernet")
                                ETH2o++;
                            if (packetQueue2[0].Data[12] == 8 && packetQueue2[0].Data[13] == 6)
                                ARP2o++;
                            if (packetQueue2[0].Data[12] == 8 && packetQueue2[0].Data[13] == 0)
                            {
                                IP2o++;
                                if (packetQueue2[0].Data[23] == 6)
                                {
                                    TCP2o++;
                                    if (packetQueue2[0].Data[34] == 0 && packetQueue2[0].Data[35] == 80 || packetQueue2[0].Data[36] == 0 && packetQueue2[0].Data[37] == 80)
                                        HTTP2o++;
                                }
                                else if (packetQueue2[0].Data[23] == 17)
                                    UDP2o++;
                                else if (packetQueue2[0].Data[23] == 1)
                                    ICMP2o++;
                            }
                        }
                    }
                    packetQueue2.RemoveAt(0);
                    updateStatistics();
                }
                Thread.Sleep(10);
            }
            //device.StopCapture();
            //device.Close();
        }
        private void InterfaceTimer()
        {
            while (true)
            {
                for (int i = 0; i < macTable.Count; i++)
                {
                    macTable[i][2] = (int)macTable[i][2] - 1;
                    if ((int)macTable[i][2] <= 0)
                        macTable.RemoveAt(i);

                    else if (macTable[i][0].Equals("00:50:79:66:68:03"))
                    {
                        if ((int)macTable[i][2] < timeOut - 5)
                        {
                            clearMacTable(1);
                            int1connect = false;
                        }
                        else
                            int1connect = true;
                    }
                    else if (macTable[i][0].Equals("00:50:79:66:68:04"))
                    {
                        if ((int)macTable[i][2] < timeOut - 5)
                        {
                            clearMacTable(2);
                            int2connect = false;
                        }
                        else
                            int2connect = true;
                    }
                }
                updateMacTable();
                Thread.Sleep(1000);
            }
        }

        public int findMacRecord(string macAddress)
        {
            for (int i = 0; i < macTable.Count; i++)
                if (macTable[i][0].Equals(macAddress))
                    return (int)macTable[i][1];
            return 0;
        }
        public void clearMacTable(int port)
        {
            for (int i = 0; i < macTable.Count; i++)
                if ((int)macTable[i][1] == port)
                {
                    macTable.RemoveAt(i);
                    i--;
                }
        }
        public void updateStatistics()
        {
            if (IsHandleCreated)
                this.Invoke((MethodInvoker)delegate ()
                {
                    ETH1in.Text = ETH1i.ToString(); ETH1out.Text = ETH1o.ToString(); ETH2in.Text = ETH2i.ToString(); ETH2out.Text = ETH2o.ToString();
                    IP1in.Text = IP1i.ToString(); IP1out.Text = IP1o.ToString(); IP2in.Text = IP2i.ToString(); IP2out.Text = IP2o.ToString();
                    ARP1in.Text = ARP1i.ToString(); ARP1out.Text = ARP1o.ToString(); ARP2in.Text = ARP2i.ToString(); ARP2out.Text = ARP2o.ToString();
                    TCP1in.Text = TCP1i.ToString(); TCP1out.Text = TCP1o.ToString(); TCP2in.Text = TCP2i.ToString(); TCP2out.Text = TCP2o.ToString();
                    UDP1in.Text = UDP1i.ToString(); UDP1out.Text = UDP1o.ToString(); UDP2in.Text = UDP2i.ToString(); UDP2out.Text = UDP2o.ToString();
                    ICMP1in.Text = ICMP1i.ToString(); ICMP1out.Text = ICMP1o.ToString(); ICMP2in.Text = ICMP2i.ToString(); ICMP2out.Text = ICMP2o.ToString();
                    HTTP1in.Text = HTTP1i.ToString(); HTTP1out.Text = HTTP1o.ToString(); HTTP2in.Text = HTTP2i.ToString(); HTTP2out.Text = HTTP2o.ToString();
                    Total1in.Text = (ETH1i).ToString(); Total1out.Text = (ETH1o).ToString(); Total2in.Text = (ETH2i).ToString(); Total2out.Text = (ETH2o).ToString();
                }
                );
        }
        public void updateMacTable()
        {
            if (IsHandleCreated)
                this.Invoke((MethodInvoker)delegate ()
                {
                    macTableList.Items.Clear();
                    for (int i = 0; i < macTable.Count; i++)
                    {
                        macTableList.Items.Add(macTable[i][0].ToString());
                        macTableList.Items[i].SubItems.Add(macTable[i][1].ToString());
                        macTableList.Items[i].SubItems.Add(macTable[i][2].ToString());

                    }
                    if (int1connect)
                    {
                        Int1Connect.ForeColor = Color.Lime;
                    }
                    else
                    {
                        Int1Connect.ForeColor = Color.Red;
                    }
                    if (int2connect)
                    {
                        Int2Connect.ForeColor = Color.Lime;
                    }
                    else
                    {
                        Int2Connect.ForeColor = Color.Red;
                    }
                });
        }
        public void updateFilterTable()
        {
            if (IsHandleCreated)
                this.Invoke((MethodInvoker)delegate ()
                {
                    filterListView.Items.Clear();
                    for (int i = 0; i < filterList.Count; i++)
                    {
                        filterListView.Items.Add(filterList[i][0].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][1].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][2].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][3].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][4].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][5].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][6].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][7].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][8].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][9].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][10].ToString());
                        filterListView.Items[i].SubItems.Add(filterList[i][11].ToString());
                    }
                });
        }
        public bool checkFilter(int port,String direction, RawCapture rawPacket)
        {
            bool pass = true;
            bool permit;
            byte[] packet = rawPacket.Data;
            String icmpType = "";

            for (int i = filterList.Count-1; i >= 0; i--)
            {
                permit = (bool)filterList[i][1];
                if (!filterList[i][11].Equals("Any"))
                {
                    icmpType = (String)filterList[i][11];
                    icmpType = icmpType.Substring(0, 2);              
                }
                if((int)filterList[i][2] == port && filterList[i][3].Equals(direction)
                && (!filterList[i][4].Equals("Any") && getSourceMacAddress(packet).Equals(filterList[i][4]))
                && (!filterList[i][5].Equals("Any") && getDestMacAddress(packet).Equals(filterList[i][5]))
                && (!filterList[i][6].Equals("Any") && getSourceIpAddress(packet).Equals(filterList[i][6]))
                && (!filterList[i][7].Equals("Any") && getDestIpAddress(packet).Equals(filterList[i][7]))
                && (!filterList[i][8].Equals("Any") 
                || ((packet[23] == 6 && filterList[i][8].Equals("TCP")) && ((filterList[i][9].Equals("Any") || (Convert.ToInt32(("0x" + packet[34].ToString("X") + packet[35].ToString("X")), 16) == int.Parse((String)filterList[i][9]))) && (filterList[i][10].Equals("Any") || ((Convert.ToInt32(("0x" + packet[34].ToString("X") + packet[35].ToString("X")), 16) == int.Parse((String)filterList[i][10]))))))
                || ((packet[23] == 17 && filterList[i][8].Equals("UDP")) && ((filterList[i][9].Equals("Any") || (Convert.ToInt32(("0x" + packet[34].ToString("X") + packet[35].ToString("X")), 16) == int.Parse((String)filterList[i][9]))) && (filterList[i][10].Equals("Any") || ((Convert.ToInt32(("0x" + packet[34].ToString("X") + packet[35].ToString("X")), 16) == int.Parse((String)filterList[i][10]))))))
                || (packet[23] == 1 && filterList[i][8].Equals("ICMP") && (filterList[i][11].Equals("Any") || int.Parse(icmpType) == packet[34] ))))
                    pass = permit;
            }
            return pass;
        }
    }
}
