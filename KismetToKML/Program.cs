using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace KismetToKML
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string inputpath="";
            bool exportclients = false;
            bool exportaps = false;

            bool exportwep = false;
            bool exportwpa = false;
            bool exportopen = false;

            if (args.Where(arg => arg.EndsWith(".kismet")).Count() == 0)
            {
                Console.WriteLine("-c      Export Clients");
                Console.WriteLine("-ap     Export AP's");
                Console.WriteLine("-wep    Export WEP ap's");
                Console.WriteLine("-wpa    Export WPA ap's");
                Console.WriteLine("-open   Export open ap's");

                Console.WriteLine("If no switch is set everything is exported");

                Environment.Exit(0);
            }
            else
            {
                inputpath = args[0];
            }

            if (args.Contains("-ap"))
                exportaps = true;

            if (args.Contains("-c"))
                exportclients = true;

            if (args.Contains("-wep"))
                exportwep = true;

            if (args.Contains("-wpa"))
                exportwpa = true;

            if (args.Contains("-open"))
                exportopen = true;


            int failedToParse = 0;

            DateTime starttime = DateTime.Now;


            List<WifiDevice> wifiDevices = new List<WifiDevice>();

            SQLiteConnection m_dbConnection = new SQLiteConnection(@"Data Source=" + inputpath + "; Version=3;");
            m_dbConnection.Open();

            string sql = "SELECT * FROM devices";

            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);

            SQLiteDataReader reader = command.ExecuteReader();

            int allDeviceCount = 0;


            while (reader.Read())
            {
                allDeviceCount++;

                try
                {
                    dynamic device = JsonConvert.DeserializeObject(System.Text.Encoding.Default.GetString((byte[])reader["device"]));


                    var deviceType = device["kismet.device.base.type"];

                    if (deviceType.ToString().Contains("Wi-Fi AP") ||
                        deviceType == "Wi-Fi Bridged" ||
                        deviceType == "Wi-Fi Ad-Hoc" ||
                        deviceType.ToString().Contains("Wi-Fi WDS"))
                    {
                        WifiAP wifiAP = new WifiAP();

                        try
                        {
                            wifiAP.SSID = device["dot11.device"]["dot11.device.advertised_ssid_map"][0]["dot11.advertisedssid.ssid"].ToString().Replace("&", "&amp;amp;");

                        }
                        catch (Exception)
                        {

                            wifiAP.SSID = "NULL";
                        }
                        wifiAP.MACAddress = device["kismet.device.base.macaddr"];
                        wifiAP.Channel = device["kismet.device.base.channel"];
                        wifiAP.Encryption = device["kismet.device.base.crypt"];
                        wifiAP.Manufactorer = device["kismet.device.base.manuf"].ToString().Replace("&", "&amp;amp;");


                        try
                        {
                            var lat = device["kismet.device.base.location"]["kismet.common.location.avg_loc"]["kismet.common.location.geopoint"][1];
                            var lon = device["kismet.device.base.location"]["kismet.common.location.avg_loc"]["kismet.common.location.geopoint"][0];

                            wifiAP.Lon = lon;
                            wifiAP.Lat = lat;
                            wifiDevices.Add(wifiAP);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Failed to parse location for " + wifiAP.MACAddress);
                            failedToParse++;
                        }

                    }
                    else if (deviceType.ToString().Contains("Wi-Fi Device"))
                    {
                        WifiClient wifiClient = new WifiClient();
                        wifiClient.MACAddress = device["kismet.device.base.macaddr"];
                        wifiClient.Manufactorer = device["kismet.device.base.manuf"].ToString().Replace("&", "&amp;amp;");

                        try
                        {
                            var lat = device["kismet.device.base.location"]["kismet.common.location.avg_loc"]["kismet.common.location.geopoint"][1];
                            var lon = device["kismet.device.base.location"]["kismet.common.location.avg_loc"]["kismet.common.location.geopoint"][0];

                            wifiClient.Lon = lon;
                            wifiClient.Lat = lat;

                            wifiDevices.Add(wifiClient);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Failed to parse location for " + wifiClient.MACAddress);
                            failedToParse++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    failedToParse++;
                }
            }



            m_dbConnection.Close();


            if(!exportaps && !exportclients  && !exportwpa && !exportwep && !exportopen)
            {
                exportaps = true;
                exportclients = true;
                exportwep = true;
                exportwpa = true;
                exportopen = true;
            }


            if(exportaps)
            {
                exportwep = true;
                exportwpa = true;
                exportopen = true;
            }


            List<WifiDevice> tempwifiDevices = new List<WifiDevice>();

            if (exportclients)
            {
                tempwifiDevices.AddRange(wifiDevices.Where(dev => dev.GetType() == typeof(WifiClient)).ToList());
            }



            if (exportwep)
            {
                tempwifiDevices.AddRange(wifiDevices.Where(dev => dev.GetType() == typeof(WifiAP) && ((WifiAP)dev).Encryption.Contains("WEP")).ToList());
            }

            if (exportwpa)
            {
                tempwifiDevices.AddRange(wifiDevices.Where(dev => dev.GetType() == typeof(WifiAP) && !((WifiAP)dev).Encryption.Contains("WEP") && !((WifiAP)dev).Encryption.ToLower().Contains("open")).ToList());
            }

            if (exportopen)
            {
                tempwifiDevices.AddRange(wifiDevices.Where(dev => dev.GetType() == typeof(WifiAP) && ((WifiAP)dev).Encryption.ToLower().Contains("open")).ToList());
            }

            wifiDevices = tempwifiDevices;

            inputpath = inputpath.Trim('"');

            string path = Path.GetDirectoryName(inputpath);


            using (StreamWriter sw = new StreamWriter(Path.Combine(Path.GetDirectoryName(inputpath), Path.GetFileNameWithoutExtension(inputpath)+".kml")))
            {
                sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sw.WriteLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\" xmlns:gx=\"http://www.google.com/kml/ext/2.2\">");
                sw.WriteLine("<Document id=\"1\">");

                sw.WriteLine("<name>Kismet</name>");


                using (StreamReader sr = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mapStyle.xml")))
                {
                    sw.Write(sr.ReadToEnd());
                }




                foreach (var device in wifiDevices)
                {
                    if (device.GetType() == typeof(WifiAP))
                    {
                        WifiAP wifiAP = (WifiAP)device;

                        sw.WriteLine("<Placemark id=\"" + wifiDevices.IndexOf(device) + "\">");
                        sw.WriteLine("<name>" + (wifiAP.SSID =="" ? wifiAP.MACAddress : wifiAP.SSID) + "</name>");
                        sw.WriteLine("<description>");
                        sw.WriteLine("MAC: " + wifiAP.MACAddress);
                        sw.WriteLine("Manufacturer: " + wifiAP.Manufactorer == "" ? "Unknown" : wifiAP.Manufactorer);
                        sw.WriteLine("Channel: " + wifiAP.Channel);
                        sw.WriteLine("Encryption: " + wifiAP.Encryption);
                        sw.WriteLine("</description>");

                        if(wifiAP.Encryption.Contains("WEP"))
                        {
                            sw.WriteLine("<styleUrl>#icon-1895-880E4F-nodesc</styleUrl>");
                        }
                        else if (wifiAP.Encryption.Contains("Open"))
                        {
                            sw.WriteLine("<styleUrl>#icon-1895-0F9D58-nodesc</styleUrl>");
                        }
                        else
                            sw.WriteLine("<styleUrl>#icon-1895-0288D1-nodesc</styleUrl>");



                        sw.WriteLine("<Point id=\"" + wifiDevices.IndexOf(device) + "\">");
                        sw.WriteLine("<coordinates>" + wifiAP.Lon + "," + wifiAP.Lat + "</coordinates>");
                        sw.WriteLine("</Point></Placemark>");
                    }
                    else
                    {
                        WifiClient wifiClient = (WifiClient)device;

                        sw.WriteLine("<Placemark id=\"" + wifiDevices.IndexOf(device) + "\">");
                        sw.WriteLine("<name>" + wifiClient.MACAddress + "</name>");
                        sw.WriteLine("<description>");
                        sw.WriteLine("Manufacturer: " + wifiClient.Manufactorer==""?"Unknown": wifiClient.Manufactorer);
                        sw.WriteLine("</description>");
                        sw.WriteLine("<Point id=\"" + wifiDevices.IndexOf(device) + "\">");

                        sw.WriteLine("<styleUrl>#icon-1751-0288D1-nodesc</styleUrl>");

                        sw.WriteLine("<coordinates>" + wifiClient.Lon + "," + wifiClient.Lat + "</coordinates>");
                        sw.WriteLine("</Point></Placemark>");
                    }

                }

                sw.WriteLine("</Document></kml>");
            }




            Console.WriteLine("Devices in input file:");
            Console.WriteLine(allDeviceCount);

            Console.WriteLine("Devices failed to parse:");
            Console.WriteLine(failedToParse);

            Console.WriteLine("Devices in output file:");
            Console.WriteLine(wifiDevices.Count);


            Console.WriteLine("Time spend:");
            Console.WriteLine(DateTime.Now - starttime);

       
        }
    }



    public class WifiDevice
    {
        public string Lat { get; set; }
        public string Lon { get; set; }
    }



    public class WifiClient :WifiDevice
    {
        public string MACAddress { get; set; }
        public string Manufactorer { get; set; }

        public WifiClient()
        {

        }

        public WifiClient(string macAddress, string manufactorer, string lat, string lon)
        {
            MACAddress = macAddress;
            Manufactorer = manufactorer;
            Lat = lat;
            Lon = lon;
        }
    }

    public class WifiAP : WifiDevice
    {
        public string SSID { get; set; }
        public string Channel { get; set; }
        public string MACAddress { get; set; }
        public string Manufactorer { get; set; }
        public string Encryption { get; set; }

        public WifiAP()
        {

        }

        public WifiAP(string ssid,string channel,string macAddress,string manufactorer, string encryption, string lat, string lon)
        {
            SSID = ssid;
            Channel = channel;
            MACAddress = macAddress;
            Manufactorer= manufactorer;
            Encryption = encryption;
            Lat = lat;
            Lon = lon;
        }
    }
}