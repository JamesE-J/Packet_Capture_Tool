﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpPcap;
using PacketDotNet;
using Packet_Capture_Tool;
using System.Net;
using SnmpSharpNet;

namespace Packet_Capture_Tool
{
    public partial class Form1 : Form
    {
        List<string> captureDeviceList;
        int deviceIndex;
        bool isPromisc;
        CaptureDeviceList devices;
        int typeOfDecode = 0;
        string writeLine;
        bool stopCapture = false;
        int filterMode = 0;
        bool decodeMode;

        public Form1()
        {
            InitializeComponent();
            this.button1.Click += new EventHandler(button1_Click);
            this.button2.Click += new EventHandler(button2_Click);
            this.button3.Click += new EventHandler(button3_Click);
            this.button4.Click += new EventHandler(button4_Click);
            this.radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
            this.radioButton2.CheckedChanged += new EventHandler(radioButton2_CheckedChanged);
            this.radioButton3.CheckedChanged += new EventHandler(radioButton3_CheckedChanged);
            string displayText = get_Device_List();
            textBox1.Text = displayText;
            comboBox1.DataSource = captureDeviceList;
            button3.Enabled = false;
            button4.Enabled = false;
        }

        private string get_Device_List()
        {
            captureDeviceList = new List<string>();
            string ver = SharpPcap.Version.VersionString;
            string stringDevices = "SharpPcap {0}, Example1.IfList.cs" + ver;
            devices = CaptureDeviceList.Instance;
            if (devices.Count < 1)
            {
                stringDevices = stringDevices + Environment.NewLine + "No devices were found on this machine";
                return stringDevices;
            }
            stringDevices = stringDevices + Environment.NewLine + "The following devices are available on this machine: ";
            int count = 0;
            foreach (ICaptureDevice dev in devices)
            {
                string device = count.ToString();
                stringDevices = stringDevices + Environment.NewLine + "----------------------------------------------------------------------" + Environment.NewLine + "Device #" + device + ": " + dev.ToString();
                captureDeviceList.Add(device);
                count++;
            }

            return stringDevices;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            deviceIndex = (int)comboBox1.SelectedIndex;
            isPromisc = (bool)checkBox1.Checked;
            decodeMode = false;
            Thread l = new Thread(new ThreadStart(listen_Start));
            l.IsBackground = true;
            l.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            stopCapture = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            deviceIndex = (int)comboBox1.SelectedIndex;
            isPromisc = (bool)checkBox1.Checked;
            decodeMode = true;
            Thread l = new Thread(new ThreadStart(listen_Start));
            l.IsBackground = true;
            l.Start();
        }
        private void button4_Click(object sender, EventArgs e)
        {
            stopCapture = true;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            typeOfDecode = 0;
            button3.Enabled = false;
            button4.Enabled = false;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            typeOfDecode = 1;
            button3.Enabled = true;
            button4.Enabled = true;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            typeOfDecode = 2;
            button3.Enabled = false;
            button4.Enabled = false;
        }

        private void updateLog()
        {
            textBox1.AppendText(Environment.NewLine + writeLine);
        }

        private void listen_Start()
        {
            ICaptureDevice device = devices[deviceIndex];

            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(device_OnPacketArrival);

            int readTimeoutMilliseconds = 1000;
            if (isPromisc == true) {
                device.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
            }
            else
            {
                device.Open(DeviceMode.Normal, readTimeoutMilliseconds);
            }

            string filter;

            if (decodeMode == false)
            {
                switch (typeOfDecode)
                {
                    case 0:
                        filterMode = 0;
                        break;

                    case 1:
                        filter = "ip and udp";
                        filterMode = 1;
                        device.Filter = filter;
                        break;

                    case 2:
                        filter = "ip and tcp";
                        filterMode = 2;
                        device.Filter = filter;
                        break;
                }
            }
            else
            {
                filter = "udp port 161 or udp port 162";
                device.Filter = filter;
            }

            device.StartCapture();
            writeLine = "--- Listening For Packets ---";
            Invoke(new MethodInvoker(updateLog));
            while (stopCapture == false)
            {

            }
            device.Close();
            writeLine = " -- Capture stopped, device closed. --";
            Invoke(new MethodInvoker(updateLog));
            stopCapture = false;
        }

        private void device_OnPacketArrival(object sender, CaptureEventArgs packet)
        {
            Packet pack = Packet.ParsePacket(packet.Packet.LinkLayerType, packet.Packet.Data);
            TcpPacket tcpPacket = TcpPacket.GetEncapsulated(pack);

            DateTime time = packet.Packet.Timeval.Date;
            int len = packet.Packet.Data.Length;

            if (tcpPacket != null)
            {
                IpPacket ipPacket = (IpPacket)tcpPacket.ParentPacket;
                IPAddress srcIp = ipPacket.SourceAddress;
                IPAddress dstIp = ipPacket.DestinationAddress;
                ushort srcPort = tcpPacket.SourcePort;
                ushort dstPort = tcpPacket.DestinationPort;

                writeLine = (String.Format("TCP Packet: {0}:{1}:{2},{3} Len={4} {5}:{6} -> {7}:{8}",
                                    time.Hour, time.Minute, time.Second, time.Millisecond, len,
                                    srcIp, srcPort, dstIp, dstPort));
                Invoke(new MethodInvoker(updateLog));
            }
            else
            {
                UdpPacket udpPacket = UdpPacket.GetEncapsulated(pack);
                time = packet.Packet.Timeval.Date;
                len = packet.Packet.Data.Length;
                if (udpPacket != null)
                {
                    IpPacket ipPacket = (IpPacket)udpPacket.ParentPacket;
                    IPAddress srcIp = ipPacket.SourceAddress;
                    IPAddress dstIp = ipPacket.DestinationAddress;
                    ushort srcPort = udpPacket.SourcePort;
                    ushort dstPort = udpPacket.DestinationPort;
                    writeLine = (String.Format("UDP Packet: {0}:{1}:{2},{3} Len={4} {5}:{6} -> {7}:{8}",
                                    time.Hour, time.Minute, time.Second, time.Millisecond, len,
                                    srcIp, srcPort, dstIp, dstPort));
                    Invoke(new MethodInvoker(updateLog));
                    if (decodeMode == true)
                    {
                        byte[] packetBytes = udpPacket.PayloadData;
                        int version = SnmpPacket.GetProtocolVersion(packetBytes, packetBytes.Length);
                        switch(version)
                        {
                            case (int)SnmpVersion.Ver1:
                                SnmpV1Packet snmpPacket = new SnmpV1Packet();
                                try
                                {
                                    snmpPacket.decode(packetBytes, packetBytes.Length);
                                    writeLine = "SNMP.V1 Packet: " + snmpPacket.ToString();
                                }
                                catch (Exception e)
                                {
                                    writeLine = e.ToString();
                                }
                                break;
                            case (int)SnmpVersion.Ver2:
                                SnmpV2Packet snmp2Packet = new SnmpV2Packet();
                                try
                                {
                                    snmp2Packet.decode(packetBytes, packetBytes.Length);
                                    writeLine = "SNMP.V2 Packet: " + snmp2Packet.ToString();
                                }
                                catch (Exception e)
                                {
                                    writeLine = e.ToString();
                                }
                                break;
                            case (int)SnmpVersion.Ver3:
                                SnmpV3Packet snmp3Packet = new SnmpV3Packet();
                                try
                                {
                                    snmp3Packet.decode(packetBytes, packetBytes.Length);
                                    writeLine = "SNMP.V3 Packet: " + snmp3Packet.ToString();
                                }
                                catch (Exception e)
                                {
                                    writeLine = e.ToString();
                                }
                                break;
                        }
                        Invoke(new MethodInvoker(updateLog));
                    }
                }
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
    }
}
