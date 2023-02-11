
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SingularityGroup.HotReload.Editor {
    public class IpHelper {
        public static string GetIpAddress() {
            var ip = GetLocalIPv4(NetworkInterfaceType.Wireless80211);
            if (string.IsNullOrEmpty(ip)) {
                return GetLocalIPv4(NetworkInterfaceType.Ethernet);
            }
            return ip;
        }
        
        public static string GetLocalIPv4(NetworkInterfaceType _type) {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces()) {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up) {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses) {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && IsLocalIp(ip.Address.MapToIPv4().GetAddressBytes())) {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }

        // https://datatracker.ietf.org/doc/html/rfc1918#section-3
        static bool IsLocalIp(byte[] ipAddress) {
            return ipAddress[0] == 10
                || ipAddress[0] == 172
                && ipAddress[1] >= 16
                && ipAddress[1] <= 31
                || ipAddress[0] == 192
                && ipAddress[1] == 168;
        }
    }
}