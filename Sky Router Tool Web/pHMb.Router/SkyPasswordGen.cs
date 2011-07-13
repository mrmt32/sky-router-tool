using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace pHMb.Router
{
    public static class SkyPasswordGen
    {
        public static SkyDetails GetInformation(string macAddress, string serial)
        {
            // This is for the V2 netgear (black, sky-styled)
            // The algo is similar, but the serial is added to the mac address after a -
            // The wifi password uses an SHA-1 hash instead of the MD5 also

            SkyDetails details = new SkyDetails();

            // First calculate MD5 of the mac address
            MD5CryptoServiceProvider md5Crypt = new MD5CryptoServiceProvider();
            byte[] bMacSerial = Encoding.ASCII.GetBytes(macAddress + "-" + serial);
            byte[] macSerialMd5 = md5Crypt.ComputeHash(bMacSerial);

            // Get SHA-1 sum of the mac address and serial
            SHA1CryptoServiceProvider sha1Crypt = new SHA1CryptoServiceProvider();
            byte[] macSerialSha1 = sha1Crypt.ComputeHash(bMacSerial);

            details.Password = GetPPPPassword(macSerialMd5);
            details.WifiPassword = GetWifiPassword(macSerialSha1); // << SHA-1 instead of MD5
            details.WifiSSID = GetWifiSSID(macSerialMd5);
            details.WifiChannel = GetWifiChannel(macSerialMd5);

            // The username is just the mac address with @skydsl on the end!
            details.Username = string.Format("{0}@skydsl", macAddress);

            return details;
        }

        public static SkyDetails GetInformation(string macAddress)
        {
            SkyDetails details = new SkyDetails();

            // First calculate MD5 of the mac address
            MD5CryptoServiceProvider md5Crypt = new MD5CryptoServiceProvider();
            byte[] bMacAddress = Encoding.ASCII.GetBytes(macAddress);
            byte[] macAddressMd5 = md5Crypt.ComputeHash(bMacAddress);

            details.Password = GetPPPPassword(macAddressMd5);
            details.WifiPassword = GetWifiPassword(macAddressMd5);
            details.WifiSSID = GetWifiSSID(macAddressMd5);
            details.WifiChannel = GetWifiChannel(macAddressMd5);

            // The username is just the mac address with @skydsl on the end!
            details.Username = string.Format("{0}@skydsl", macAddress);

            return details;
        }

        private static string GetPPPPassword(byte[] hash)
        {
            // The password is (bytes 0-4 + bytes 7-11), any byte that is over 0x9F in value loops round (0x100 = 0, 0x101 = 1 etc)
            StringBuilder passwordBuilder = new StringBuilder(10);
            for (int i = 0; i < 5; i++)
            {
                passwordBuilder.AppendFormat("{0:X2}", (hash[i] + hash[i + 7]) % 0x100);
            }
            return passwordBuilder.ToString().ToLower();
        }

        private static string GetWifiPassword(byte[] hash)
        {
            // The wifi password is calculated using the odd bytes
            // A byte equal to 0x00 is A, 0x01 is B etc.
            // After reaching 0x1A (Z) it loops back so 0x1B is A again
            char[] letterArray = new char[]
            {
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
            };
            StringBuilder wifiPasswordBuilder = new StringBuilder(8);
            for (int i = 1; i < 16; i = i + 2)
            {
                wifiPasswordBuilder.AppendFormat("{0}", letterArray[hash[i] % 26]);
            }
            return wifiPasswordBuilder.ToString();
        }

        private static string GetWifiSSID(byte[] hash)
        {
            // The wifi SSID number is made from the last 5 bytes, it works in a similar way to the wifi password
            // 0x00 is 0, 0x01 is 1 etc, and it loops once you get to 0x09
            // There are 10 possible values (0, 1, 2, 3, 4, 5, 6, 7, 8, 9) so we use % 10
            StringBuilder wifiSSIDBuilder = new StringBuilder(8);
            for (int i = 11; i < 16; i++)
            {
                wifiSSIDBuilder.AppendFormat("{0:G0}", hash[i] % 10);
            }
            return string.Format("SKY{0}", wifiSSIDBuilder);
        }

        private static int GetWifiChannel(byte[] hash)
        {
            // The wifi channel is calculated using the last byte. 0x00 means channel 1, 0x01 means channel 6 and 0x02 means channel 11
            // It then loops as before
            int[] channelArray = new int[]
            {
                1, 6, 11
            };
            return channelArray[hash[15] % 3];
        }

        public static string SagemTest(string inputString)
        {
            // MD5 of some combination of base mac (lowercase without :) and wifi key, maybe some other constants?
            // This is then base64 encoded, first 15 chars is password

            MD5CryptoServiceProvider md5Crypt = new MD5CryptoServiceProvider();
            byte[] bInput = Encoding.ASCII.GetBytes(inputString);
            byte[] inputMd5 = md5Crypt.ComputeHash(bInput);

            ToBase64Transform base64 = new ToBase64Transform();
            byte[] md5Base64 = new byte[24];
            base64.TransformBlock(inputMd5, 0, inputMd5.Length, md5Base64, 0);

            return Encoding.ASCII.GetString(md5Base64, 0, md5Base64.Length);
        }
    }

    public class SkyDetails
    {
        public string Password;
        public string Username;
        public string WifiPassword;
        public string WifiSSID;
        public int WifiChannel;
    }
}
