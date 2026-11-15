using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
static class VpnStatus {
 static readonly object gate=new object();static DateTime sampled;static string cached;
 static readonly Regex Names=new Regex("vpn|tailscale|wireguard|wintun|nordlynx|proton|mullvad|openvpn|anyconnect|globalprotect|fortinet|cisco|zerotier|hamachi|cloudflare|\\bwarp\\b|\\btap\\b",RegexOptions.IgnoreCase|RegexOptions.Compiled);
 public static string Read(){lock(gate){if(cached!=null&&DateTime.UtcNow-sampled<TimeSpan.FromSeconds(3))return cached;try{var names=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&(Names.IsMatch(n.Name+" "+n.Description)||n.NetworkInterfaceType==NetworkInterfaceType.Ppp)&&n.GetIPProperties().UnicastAddresses.Any(a=>(a.Address.AddressFamily==AddressFamily.InterNetwork&&!a.Address.ToString().StartsWith("169.254."))||(a.Address.AddressFamily==AddressFamily.InterNetworkV6&&!a.Address.IsIPv6LinkLocal))).Select(n=>n.Name).Distinct().ToArray();cached=new JavaScriptSerializer().Serialize(new{vpnStatus=new{connected=names.Length>0,names=names}});}catch{cached="{\"vpnStatus\":{\"connected\":false,\"names\":[],\"error\":\"VPN status unavailable\"}}";}sampled=DateTime.UtcNow;return cached;}}
 public static string Open(){System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:network-vpn"){UseShellExecute=true});return "{\"ok\":true}";}
}
