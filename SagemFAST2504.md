# Chipset #

The chipset in this router is the same as that in the first (Netgear DG834GT) sky router; the Broadcom BCM6348. This contains a 256MHz MIPS CPU, ADSL2+ capabilities and USB 1.1 support. There is no USB port fitted on this board but there is an unpopulated position for a USB B port.

The board contains a 4096KB SST39VF3201 flash chip which holds the bootloader, firmware and persistent storage.

# Bootloader #

This router uses a variant of Broadcom's CFE bootloader. Firmware can be flashed through a web interface which becomes active if either the current firmware is corrupt, the base mac address is blank or the CFE command line is entered (see the serial port section). The default IP address for this interface is 192.168.1.1.
The recovery interface accepts slightly different firmware formats depending on the board id and customer id settings:

**Board ID: FAST2404, Customer ID: Sagm**

With these settings the firmware must be consist of a tag following the "Version 3.10+" format on http://wiki.openwrt.org/doc/techref/brcm63xx.imagetag followed by the root filesystem and kernel. Flashing the CFE in this way may also be possible but I have not tested this, this is also not recommended as a bad CFE flash will brick your router (I have yet to find any form of JTAG connection on this router which is the only way to flash without a bootloader).

**Board ID: 2504, Customer ID: Sky (default)**

The 'Header CRC' field for these firmwares is different, I have yet to work out what it represents. For now only official sky firmwares can be flashed using this method.

# Web Interface Firmware Upgrade #
Both the Sky and Sagem firmwares offer a firmware upgrade option in their web interfaces. These accept a full flash image (basically a copy of the flash chip) with an appended CRC of the whole image. The images is comprised as below (CFE is padded with 0xFF at end to make up 0xFFFF bytes):
| 0x0 - 0xFFFF | 0x010000 - 0x0100FF | 0x010100+ | End of Image |
|:-------------|:--------------------|:----------|:-------------|
| CFE          | Broadcom Tag        | Root FS + Kernel | CRC of image followed by 16 x 0x00 |

I have attempted to add persistent storage data to the appropriate locations in the image, but the firmware seems to discard these. The whole flash is none the less completely overwritten however; both the persistent storage and backup areas are filled with 0xFF, wiping any previous data. This is a problem for sky routers as the backup area must contain the original wireless key in order for the PPPoA login details to be calculated by Sky's firmware. A blank configuration could also cause some unpredictable problems.

This can be overcome in custom firmwares by running a script on first boot which restores these configuration areas.

# Firmware Editing #
Unfortunately both Sky and Sagem have not released sources to any of their firmwares (even though they are required to by the GPL). This makes making any changes to the kernel next to impossible. It is however quite easy to change the file system, this consists of a LZMA compressed SquashFS image. Tools to extract and create these images can be found at http://svn.gna.org/svn/openbox4/trunk/firmware_2.x/tools/nb4-squashfs/squashfs3.4/. These tools can be compiled for windows using cygwin but you will probably encounter problems with permissions, it is recommended you do any file editing in linux.

In order to extract the root file system from the firmware image you can use my firmware tool (http://www.skyuser.co.uk/forum/technical-discussion/32854-firmware-image-editing-tool-dg834gt-sagem-f-st-broadcom-based-routers.html). It does not work with Sky's auto-update firmware format currently, these files are have a standard broadcom firmware format but have an added signature at the beginning that must be removed first. This can be done with a hex editor, just delete everything before "6   SAGM".

**Startup Scripts**

Getting your own scripts/applications to run at start up can be a problem, all of the default startup procedure is handled by the /linuxrc file which is compiled into busybox. Without sources this would be hard to change. My method was to rename /bin/cfm (this is the process which handles most of the functions on sky/sagem firmwares) to /bin/cfe2 and create a shell script in its place. This script can then run any custom applications before executing /bin/cfm2.

**Persistent Storage**

As mentioned earlier, flashing using the web interface overwrites the persistent storage areas. These therefore need to be restored. This can be done using the "fast" command. Unfortunately this command is password protected; the password for fast on firmware sky1.5 is "Fast.CPE". For later firmwares the password has been changed and is currently unknown. You therefore need to replace fast with an older version to gain write access.

```
To write backup area: fast backup-config write -p Fast.CPE < config.xml

To write current area: fast psi-config write -p Fast.CPE < config.xml

To write base mac: echo "00:11:22:33:44:55" | fast base-mac write -p Fast.CPE
```

Replacing with your configuration file and mac respectively.

# Persistent Storage Information (PSI) #
All of the router's settings (with the exception of CFE config e.g. board id, customer id and base mac address) are stored in two areas of flash. One is a backup which is used to restore the configuration to default values, this is also used by sky firmwares to read the default wireless SSID (see [SagemFAST2504#Sky\_Auto-Update](SagemFAST2504#Sky_Auto-Update.md)).

The configuration is formatted as an XML file, an example is shown below:

```
<psitree>
  <WirelessCfg>
    <vars state="enabled" hide="0" wlAdvancedMode="1" ssIdIndex="0"
    country="GB" fltMacMode="disabled" apMode="ap"
    bridgeRestrict="disabled" wdsMAC_0="" wdsMAC_1="" wdsMAC_2=""
    wdsMAC_3="" apIsolation="off" band="b" channel="0" rate="auto"
    multicastRate="auto" basicRate="default" fragThreshold="2346"
    RTSThreshold="2347" DTIM="1" beacon="100" XPress="off"
    gMode="auto" gProtection="auto" preamble="long"
    AfterBurner="off" WME="off" WMENoAck="off" TxPowerPercent="100"
    RegulatoryMode="off" PreNetRadarDectTime="60"
    InNetRadarDectTime="60" TpcMitigation="0" AutoChannelTimer="0"
    MaxAssoc="128" />
    <wlMssidVars tableSize="2">
      <wlMssidEntry enblSsId="1" ssId="" authMode="psk"
      radiusServerIP="0.0.0.0" radiusServerPort="1812"
      radiusServerKey="" wep="disabled" auth="0" keyBit="128-bit"
      key64_1="" key64_2="" key64_3="" key64_4="" key64Index="1"
      key128_1="" key128_2="" key128_3="" key128_4=""
      key128Index="1" wpaRekey="0" wpakey=""
      Preauthentication="off" ReauthTimeout="36000" wpa="tkip"
      tr69cBeaconType="Basic" tr69cBasicEncryptionModes="None"
      tr69cBasicAuthenticationMode="None"
      tr69cWPAEncryptionModes="TKIPEncryption"
      tr69cWPAAuthenticationMode="PSKAuthentication"
      tr69cIEEE11iEncryptionModes="AESEncryption"
      tr69cIEEE11iAuthenticationMode="EAPAuthentication" />
      <wlMssidEntry enblSsId="0" ssId="Guest" authMode="open"
      radiusServerIP="0.0.0.0" radiusServerPort="1812"
      radiusServerKey="" wep="disabled" auth="0" keyBit="128-bit"
      key64_1="" key64_2="" key64_3="" key64_4="" key64Index="1"
      key128_1="" key128_2="" key128_3="" key128_4=""
      key128Index="1" wpaRekey="0" wpakey=""
      Preauthentication="off" ReauthTimeout="36000" wpa="tkip"
      tr69cBeaconType="Basic" tr69cBasicEncryptionModes="None"
      tr69cBasicAuthenticationMode="None"
      tr69cWPAEncryptionModes="TKIPEncryption"
      tr69cWPAAuthenticationMode="PSKAuthentication"
      tr69cIEEE11iEncryptionModes="AESEncryption"
      tr69cIEEE11iAuthenticationMode="EAPAuthentication" />
    </wlMssidVars>
  </WirelessCfg>
  <SystemInfo>
    <protocol autoScan="enable" upnp="enable" igmpSnp="disable"
    igmpMode="disable" macFilterPolicy="forward"
    encodePassword="enable" siproxd="disable" enetwan="disable"
    enblUsbM2u="disable" enblWlM2u="disable" enblEthM2u="disable"
    enblEth0M2u="disable" enblEth1M2u="disable" />
    <sysLog state="enable" displayLevel="CRIT" logLevel="DEBUG"
    option="local" blocksite="disable" webconn="disable"
    serverIP="0.0.0.0" serverPort="514" />
    <sysUserName value="admin" >
    <GUIId value="2" >
    <LockSagemWebUI value="0" >
    <skyInfo maxDailyCheck="3" waitMin="0" waitMax="600"
    waitFix="300" fvcUrl1="http://fast2504.skyfirmware.com"
    fvcUrl2="http://fast2504.skyfirmware.com"
    postUrl="http://fast2504.skyfirmware.com/status/" />
    <adminCfgString value="SystemInfo(3,4,5,6,7,8,10,12,13,14,15,16,17,18,19,20,21,22,23)|AtmCfgTd|AtmCfg|ADSL|RouteCfg|PMapCfg|SecCfg(10,11,12,13,14,15,16,19,20,21,22)|SNTPCfg|WirelessCfg(6,7,8,9)|wan|CertCfg|EngDbgCfg" >
    <userCfgString value="SystemInfo(1,2,9,11,24,25,26)|Lan|SecCfg(1,2,3,4,7,8,9,17,18,23,24,25,26,27)|DDNSCfg|WirelessCfg(1,2,3,4,5)" >
    <ConfigId value="FAST2504_standard_13.conf" >
    <sysPassword value="c2t5" >
    <sptPassword value="c3VwcG9ydA==" >
    <usrPassword value="dXNlcg==" >
    <remoteManagement state="0" accessType="2" singleIp="0.0.0.0"
    startIp="0.0.0.0" finishIp="0.0.0.0" port="8080" />
  </SystemInfo>
  <AtmCfg>
    <initCfg structureId="2" threadPriority="25" freeCellQSize="10"
    freePktQSize="200" freePktQBufSize="1600"
    freePktQBufOffset="32" rxCellQSize="10" rxPktQSize="200"
    txFifoPriority="64" aal5MaxSduLen="64" aal2MaxSduLen="0" />
  </AtmCfg>
  <AtmCfgTd>
    <td1 cat="UBR" PCR="0" SCR="0" MBS="0" >
  </AtmCfgTd>
  <AtmCfgVcc>
    <vccId9999 vpi="0" vci="65534" tdId="0" aalType="AAL2"
    adminStatus="down" encap="unknown" qos="disable"
    instanceId="1509949444" />
    <vccId1 vpi="0" vci="40" tdId="1" aalType="AAL5"
    adminStatus="up" encap="vcMuxBr8023" qos="enable"
    instanceId="1509949443" />
    <vccId2 vpi="0" vci="38" tdId="1" aalType="AAL5"
    adminStatus="up" encap="vcMuxRouted" qos="enable"
    instanceId="1509949444" />
  </AtmCfgVcc>
  <SecCfg>
    <qosMgmtCfg enableQos="enable" defaultDSCPMark="720699776"
    defaultQueue="-1" />
    <srvCtrlList ftp="disable" http="lan" icmp="enable"
    ssh="disable" telnet="lan" tftp="disable" />
    <qosQueue tableSize="3">
      <qosQueueEntry id="1" instanceId="1" status="enable"
      interface="0/38" precedence="1" />
      <qosQueueEntry id="2" instanceId="2" status="enable"
      interface="0/38" precedence="2" />
      <qosQueueEntry id="3" instanceId="3" status="enable"
      interface="0/38" precedence="3" />
    </qosQueue>
  </SecCfg>
  <Lan>
    <entry9999 address="1.1.1.1" mask="255.255.255.0"
    dhcpServer="disable" leasedTime="0" startAddr="0.0.0.0"
    endAddr="0.0.0.0" instanceId="1509949443" />
    <entry1 address="192.168.0.1" mask="255.255.255.0"
    dhcpServer="enable" leasedTime="24" startAddr="192.168.0.2"
    endAddr="192.168.0.255" instanceId="1509949441" />
  </Lan>
  <RouteCfg>
    <ripGlobal state="disable" ripIfcTableSize="1" >
    <ripIfc tableSize="1">
      <ripIfcEntry id="1" name="br0" state="disable" version="2"
      operation="active" />
    </ripIfc>
  </RouteCfg>
  <PMapCfg>
    <pmap tableSize="1">
      <pmapEntry id="1" groupName="Default" groupKey="1"
      groupStatus="1"
      ifList="wl0:2|wl0.1:3|eth0.3:4|eth0.4:5|eth0.5:6|eth0.2:7"
      vendorid0="" vendorid1="" vendorid2="" vendorid3=""
      vendorid4="" />
    </pmap>
    <pmapFlt tableSize="7">
      <pmapFltEntry id="1" instance="1" status="enable"
      bridgeRef="-1" interfaceRef="eth0" />
      <pmapFltEntry id="2" instance="2" status="enable"
      bridgeRef="1" interfaceRef="wl0" />
      <pmapFltEntry id="3" instance="3" status="enable"
      bridgeRef="1" interfaceRef="wl0.1" />
      <pmapFltEntry id="4" instance="4" status="enable"
      bridgeRef="1" interfaceRef="eth0.3" />
      <pmapFltEntry id="5" instance="5" status="enable"
      bridgeRef="1" interfaceRef="eth0.4" />
      <pmapFltEntry id="6" instance="6" status="enable"
      bridgeRef="1" interfaceRef="eth0.5" />
      <pmapFltEntry id="7" instance="7" status="enable"
      bridgeRef="1" interfaceRef="eth0.2" />
    </pmapFlt>
    <pmapIfcCfg pmapIfName="eth0" pmapIfcStatus="enable" >
  </PMapCfg>
  <SNTPCfg>
    <cfg state="enable" server1="ntp1.isp.sky.com"
    server2="ntp2.isp.sky.com"
    timezone="Greenwich Mean Time: Dublin, Edinburgh, Lisbon, London"
    offset="0" useDst="0" dstStartMonth="0" dstStartDay="0"
    dstStartDayType="0" dstStartDayTimeofDay="0" dstEndMonth="0"
    dstEndDay="0" dstEndDayType="0" dstEndDayTimeofDay="0" />
  </SNTPCfg>
  <ADSL>
    <settings G.Dmt="enable" G.lite="enable" T1.413="enable"
    ADSL2="enable" AnnexL="disable" ADSL2plus="enable"
    AnnexM="enable" pair="inner" bitswap="enable" SRA="disable" />
  </ADSL>
  <ipsrv_0_40>
    <dhcpc_conId1 state="enable" wanAddress="255.255.255.255"
    wanMask="255.255.255.255" merMtu="1500" />
  </ipsrv_0_40>
  <wan_0_40>
    <entry1 vccId="1" vlanMuxId="-1" conId="1" name="mer_0_40"
    protocol="MER" encap="VCMUX" firewall="enable" nat="enable"
    igmp="enable" vlanId="-1" service="enable"
    instanceId="1509949444" enblIptv="disable" iptvName="" />
  </wan_0_40>
  <CertCfg >
  <ToDCfg >
  <EngDbgCfg >
  <pppsrv_0_40 >
  <pppsrv_0_38>
    <ppp_conId1 userName="sky" password="c2t5" serviceName=""
    idleTimeout="0" ipExt="disable" auth="auto" useStaticIpAddr="0"
    localIpAddr="0.0.0.0" Debug="disable"
    pppAuthErrorRetry="enable" pppAuthRetryInterval="60"
    pppToBridge="disable" pppMtu="1478" />
  </pppsrv_0_38>
  <wan_0_38>
    <entry1 vccId="2" vlanMuxId="-1" conId="1" name="pppoa_0_38_1"
    protocol="PPPOA" encap="VCMUX" firewall="enable" nat="enable"
    igmp="enable" vlanId="-1" service="enable"
    instanceId="1509949445" enblIptv="disable" iptvName="" />
  </wan_0_38>
</psitree>
```

# Toolchain / Compiling applications #
You need a uclibc-crosstools toolchain to compile applications for this firmware. A pre-made toolchain is available in the gpl releases for many other routers based on the BCM63xx series of chipsets, I think the one I used came from a D-Link source code release. I was able to compile mini\_httpd quite easily for this platform. Linux (or possibly cygwin?) is required to compile applications.

# CFM #
CFM is a process which runs on both the Sky and Sagem firmwares, it controls many aspects of the router.

**Web Server**

CFM contains an micro\_httpd web server which hosts the web interface. It contains internal functions which allow the editing of the configuration data by built in cgi scripts. On Sky firmwares, if the "LockSagemWebUI" setting is set to 1, only web pages matching sky`_``*`.html are accessible. This locks out the Sagem web UI (and settings, the sky interface uses a separate cgi url with limited capabilities). The web pages are stored in standard HTML format with special tags which allow firmware values to be displayed.

**Telnet**

The CFM also contains a built-in telnet server, this is however disabled by default on sky firmwares. It is also impossible to enable from the sky web interface. Telnet access control is managed by the "srvCtrlList" setting in the persistent storage. When enabled the username and password used to login to telnet are the same as the web ui details.

# Sky Auto-Update #
Included in Sky's version of CFM is an automatic update client. This contacts Sky's server up to 3 times a day to check for a configuration or firmware update.

  1. Router contacts http://fast2504.skyfirmware.com/?mac=a1b2c3d4e5&firmwareVersion=2.8Sky&model=fast2504&product=skydsl (replacing with router's mac address and firmware version)
  1. Server replies with information for example:
```
[SAGEM2504]
downloadHost=download.skyfirmware.com
firmwarePath=/S/xxxxxxx/xxxxxxx/download/firmware/fast2504/
firmwareImage=2.8Sky
firmwareSize=3606649
configPath=/session/xxxxxxx/xxxxxx/download/config/fast2504/
configName=FAST2504_standard_13.conf
statusHost=download.skyfirmware.com
statusPath=/session/xxxxxxx/xxxxxxxx/status/
```
  1. If the firmware version is different it downloads a new firmware from the given url (http://download.skyfirmware.com/S/xxxxxxx/xxxxxxx/download/firmware/fast2504/2.8Sky in the example above) and flashes it.
  1. If the configuration name is different the router downloads the configuration file (http://download.skyfirmware.com/session/xxxxxxx/xxxxxx/download/config/fast2504/FAST2504_standard_13.conf) and updates the persistent storage area.

This means that sky can change any settings on all, or a subset of their routers (by mac address) at any time. They can also have limited firmware roll outs to only certain mac addresses.

This is also an easy way to obtain a sky firmware image for editing, just follow the above procedure to download an image.

If you have access to the Sagem web UI auto-updates can be disabled by changing the urls on http://192.168.0.1/skyinfo.html and saving.

# Serial Port #
There is an unpopulated, 4 contact header located just below the ethernet connectors on the right. From the Ethernet side to front the pin-out is:

  1. VCC
  1. RX
  1. TX
  1. GND

You need to have an RS232 -> 3.3V level converter (see http://www.skyuser.co.uk/forum/technical-discussion/32805-recovering-severly-bricked-dg834gt-adding-serial-port.html, but note the slightly different pinout on this router). A hacked phone data cable can also work.

# Sky PPP Authentication #
Unlike the DG834GT the PPP username and password are not stored in the router configuration, they are instead calculated by a function named 'AutoGeneratePPPUserAndPassword' in cfm before being passed to pppd. This seems to take some combination of the base mac address and default wireless key (saved in the backup-config area), create an md5 hash of this and then base64 encode it. The first 15 characters of the base64 encoding are used as the password.

The username is mac\_address@skydsl as with the other routers. The mac address is formatted as lower case hex without separators, eg 0a1b2c3d4e5f.

# Boot Log #

(Some identifiable parts, mac address password etc. have been removed)
```
Sagem CFE version: 1.5
Build Date: Tue Sep 11 14:01:32 CST 2007 (fanrx@svr1.sagem-szn.com)
Copyright (C) 2005-2006 Sagem communication.

Boot Address 0xbfc00000

Initializing Arena.
Initializing Devices.
Parallel flash device: name SST39VF3201, id 0x235b, size 4096KB
Enter kerSysNvRamGet(CFE)
getShareBlks: i=0, sect_size=4096, end_blk=1
kerSysNvRamGet(CFE) Mac address:11:22:33:44:55, BoardId=F@ST2504  
getShareBlks: i=1010, sect_size=4096, end_blk=1016
getShareBlks: i=1011, sect_size=4096, end_blk=1016
getShareBlks: i=1012, sect_size=4096, end_blk=1016
getShareBlks: i=1013, sect_size=4096, end_blk=1016
getShareBlks: i=1014, sect_size=4096, end_blk=1016
getShareBlks: i=1015, sect_size=4096, end_blk=1016
Backup content:[<psitree>
]
getShareBlks: i=1018, sect_size=4096, end_blk=1024
getShareBlks: i=1019, sect_size=4096, end_blk=1024
getShareBlks: i=1020, sect_size=4096, end_blk=1024
getShareBlks: i=1021, sect_size=4096, end_blk=1024
getShareBlks: i=1022, sect_size=4096, end_blk=1024
getShareBlks: i=1023, sect_size=4096, end_blk=1024
PSI content:[<psitree>
]
Reset  mii_switch_unmanage_mode configuration
CPU type 0x29107: 256MHz, Bus: 128MHz, Ref: 32MHz
Total memory: 16777216 bytes (16MB)

Total memory used by CFE:  0x80401000 - 0x80528060 (1208416)
Initialized Data:          0x8041E850 - 0x804217D0 (12160)
BSS Area:                  0x804217D0 - 0x80426060 (18576)
Local Heap:                0x80426060 - 0x80526060 (1048576)
Stack Area:                0x80526060 - 0x80528060 (8192)
Text (code) segment:       0x80401000 - 0x8041E844 (120900)
Boot area (physical):      0x00529000 - 0x00569000
Relocation Factor:         I:00000000 - D:00000000

Enter kerSysNvRamGet(CFE)
getShareBlks: i=0, sect_size=4096, end_blk=1
kerSysNvRamGet(CFE) Mac address:11:22:33:44:55, BoardId=F@ST2504  
Enter kerSysNvRamGet(CFE)
getShareBlks: i=0, sect_size=4096, end_blk=1
kerSysNvRamGet(CFE) Mac address:11:22:33:44:55, BoardId=F@ST2504  
Enter kerSysNvRamGet(CFE)
getShareBlks: i=0, sect_size=4096, end_blk=1
kerSysNvRamGet(CFE) Mac address:11:22:33:44:55, BoardId=F@ST2504  
Board IP address                  : 192.168.1.112  
Host IP address                   : 192.168.1.100  
Gateway IP address                :   
Run from flash (f)                : f  
Default host run file name        : vmlinux  
Default host flash file name      : bcm963xx_fs_kernel  
Boot delay (0-9 seconds)          : 3  
Enter kerSysNvRamGet(CFE)
getShareBlks: i=0, sect_size=4096, end_blk=1
kerSysNvRamGet(CFE) Mac address:11:22:33:44:55, BoardId=F@ST2504  
Board Id (0-11)                   : F@ST2504  
Number of MAC Addresses (1-32)    : 11  
Base MAC Address                  : 00:1e:74:86:bc:d3  
PSI Size (1-64) KBytes            : 24  
Customer Name (0-3)               : 2  

*** Press any key to stop auto run (3 seconds) ***
Auto run second count down: 33210
Enter kerSysNvRamGet(CFE)
getShareBlks: i=0, sect_size=4096, end_blk=1
kerSysNvRamGet(CFE) Mac address:11:22:33:44:55, BoardId=F@ST2504  
Code Address: 0x80010000, Entry Address: 0x801b4018
Decompression OK!
Entry at 0x801b4018
Closing network.
Starting program at 0x801b4018
Linux version 2.6.8.1 (chenc@svr1.dongguan.cn) (gcc version 3.4.2) #1 Mon Sep 13 17:51:16 CST 2010

Parallel flash device: name SST39VF3201, id 0x235b, size 4096KB

Total Flash size: 4096K with 1024 sectors

fInfo.flash_scratch_pad_start_blk = 1016

fInfo.flash_scratch_pad_number_blk = 2

fInfo.flash_scratch_pad_length = 0x2000

fInfo.flash_scratch_pad_blk_offset = 0x0


fInfo.flash_nvram_start_blk = 0

fInfo.flash_nvram_blk_offset = 0x580

fInfo.flash_nvram_number_blk = 1


fInfo.flash_persistent_start_blk = 1018

fInfo.flash_persistent_blk_offset = 0x0

fInfo.flash_persistent_number_blk = 6


backup_start_blk = 1010

backup_blk_offset = 0x0

backup_number_blk = 6

backup_length = 24576


psi startAddr = bfffa000

sp startAddr = bfff8000

backup startAddr = bfff2000


F@ST2404 prom init

CPU revision is: 00029107

Determined physical RAM map:

 memory: 00fa0000 @ 00000000 (usable)

On node 0 totalpages: 4000

  DMA zone: 4000 pages, LIFO batch:1

  Normal zone: 0 pages, LIFO batch:1

  HighMem zone: 0 pages, LIFO batch:1

Built 1 zonelists

Kernel command line: root=31:0 ro noinitrd console=ttyS0,115200

brcm mips: enabling icache and dcache...

Primary instruction cache 16kB, physically tagged, 2-way, linesize 16 bytes.

Primary data cache 8kB 2-way, linesize 16 bytes.

PID hash table entries: 64 (order 6: 512 bytes)

Using 128.000 MHz high precision timer.

Dentry cache hash table entries: 4096 (order: 2, 16384 bytes)

Inode-cache hash table entries: 2048 (order: 1, 8192 bytes)

Memory: 13900k/16000k available (1453k kernel code, 2080k reserved, 222k data, 72k init, 0k highmem)

Calibrating delay loop... 255.59 BogoMIPS

Mount-cache hash table entries: 512 (order: 0, 4096 bytes)

Checking for 'wait' instruction...  unavailable.

NET: Registered protocol family 16

Can't analyze prologue code at 80179d94

Initializing Cryptographic API

PPP generic driver version 2.4.2

NET: Registered protocol family 24

Using noop io scheduler

bcm963xx_mtd driver v1.0

brcmboard: brcm_board_init entry

RestoreMacFilter: Button GPIO 0x21 is enabled

RestoreMacFilter: Button Interrupt 0x1 is enabled

RestoreMacFilter: Interrupt mapping OK

Serial: BCM63XX driver $Revision: 1.3 $

ttyS0 at MMIO 0xfffe0300 (irq = 10) is a BCM63XX

NET: Registered protocol family 2

IP: routing cache hash table of 512 buckets, 4Kbytes

********* ip_rt_init ************

sizeof(struct rtable)=244

ip_rt_max_size=2048, rt_hash_mask=511, ipv4_dst_ops.gc_thresh=512, 

ip_rt_gc_interval=12000, rt_secret_rebuild=120000

TCP: Hash tables configured (established 512 bind 1024)

Initializing IPsec netlink socket

NET: Registered protocol family 1

NET: Registered protocol family 17

NET: Registered protocol family 15

Ebtables v2.0 registered

NET: Registered protocol family 8

NET: Registered protocol family 20

802.1Q VLAN Support v1.8 Ben Greear <greearb@candelatech.com>

All bugs added by David S. Miller <davem@redhat.com>

VFS: Mounted root (squashfs filesystem) readonly.

Freeing unused kernel memory: 72k freed


init started:  BusyBox v1.00 (2010.09.13-09:57+0000) multi-call binary
Algorithmics/MIPS FPU Emulator v1.5



BusyBox v1.00 (2010.09.13-09:57+0000) Built-in shell (msh)
Enter 'help' for a list of built-in commands.


Loading drivers and kernel modules... 

atmapi: module license 'Proprietary' taints kernel.

adsl: adsl_init entry

blaadd: blaa_detect entry

Broadcom BCMPROCFS v1.0 initialized

Broadcom BCM6348B0 Ethernet Network Device v0.3 Sep 13 2010 17:49:33

Config Ethernet Switch Through SPI Slave Select 0

dgasp: kerSysRegisterDyingGaspHandler: eth0 registered 

eth0: MAC Address: 11:22:33:44:55:66

Cfm wathcer has been setup!

PCI: Setting latency timer of device 0000:00:01.0 to 64

PCI: Enabling device 0000:00:01.0 (0004 -> 0006)

wl: srom not detected, using main memory mapped srom info (wombo board)

wl0: wlc_attach: use mac addr from the system pool by id: 0x776c0000

wl0: MAC Address: 11:22:33:44:55:66

wl0: Broadcom BCM4318 802.11 Wireless Controller 4.150.10.15.cpe2.2

dgasp: kerSysRegisterDyingGaspHandler: wl0 registered 

*****************************************
**      Firmware mod by mrmt32         **
**        mrmt32@gmail.com             **
*****************************************

Run the actual CFM
Custom Firmware > Loading mini_httpd
eth0 Link UP.

Custom Firmware > Going to sleep before running backup-checker!
Cfm has been started to initiate!

Read Flash: part=[PERSISTENT]

Read Flash: part=[PERSISTENT]

PSI has been initiated successfully!

BcmAdsl_Initialize=0xC00673D8, g_pFnNotifyCallback=0xC00808E4

AnnexCParam=0x7FFF7EB8 AnnexAParam=0x00003987 adsl2=0x00000000

pSdramPHY=0xA0FFFFF8, 0xD474C 0xDEADBEEF

AdslCoreHwReset: AdslOemDataAddr = 0xA0FFB1E4

AnnexCParam=0x7FFF7EB8 AnnexAParam=0x00003987 adsl2=0x00000000

ATM proc init !!!

ATM Config Management initiated successfully!

ip_tables: (C) 2000-2002 Netfilter core team

ip_conntrack version 2.1 (125 buckets, 0 max) - 368 bytes per conntrack

ip_conntrack_pptp version 2.1 loaded

ip_nat_pptp version 2.0 loaded

ip_conntrack_h323: init 

ip_nat_h323: initialize the module!

ADSL G.994 training

BRCM NAT Caching v0.1 Nov 18 2006 14:56:48

BRCM NAT Cache: Hooking hit function @ c0057c64

ip_nat_ftp: Unknown symbol needs_ip_conntrack_ftp

insmod: cannot insert `/lib/modules/2.6.8.1/kernel/net/ipv4/netfilter/ip_nat_ftp.ko': Unknown symbol in module (2): No such file or directory
ip_conntrack_rtsp v0.01 loading

ip_nat_rtsp v0.01 loading

atm: Queue was not found.
atm: Queue was not found.
Security configuration management initiated successfully!


==>   Software Version: 2.8Sky  <==

device eth0 entered promiscuous mode

br0: port 1(eth0) entering learning state

br0: topology change detected, propagating

br0: port 1(eth0) entering forwarding state

pm_size==[1]
pm_entry[i].grpedIntf=[wl0:2|wl0.1:3|eth0.3:4|eth0.4:5|eth0.5:6|eth0.2:7]
wlctl band b run 0 time!
Setting SSID "SKY12345"
Setting SSID "Guest"
ADSL G.992 started

Setting country code using abbreviation: "GB"
wlctl band b run 0 time!
wlctl band b run 0 time!
wlctl: page allocation failure. order:0, mode:0x20

Call Trace: [<800536bc>]  [<800534e4>]  [<8005371c>]  [<80057868>]  [<800572e8>]  [<80057b5c>]  [<c0245e38>]  [<80057750>]  [<c0245ee4>]  [<c023e070>]  [<800f9d50>]  [<c023e070>]  [<c02462e8>]  [<c0245bc4>]  [<c02462e8>]  [<c021dcbc>]  [<c01b4248>]  [<c01da810>]  [<c01b99b4>]  [<c01b981c>]  [<c01e0000>]  [<c01d9900>]  [<c0245e6c>]  [<c0245e38>]  [<c01ca74c>]  [<c020cb84>]  [<c0242a1c>]  [<c01d3e90>]  [<c01d3e70>]  [<80103a24>]  [<80020b30>]  [<80053074>]  [<8005199c>]  [<8003ea50>]  [<801c0000>]  [<8003eab8>]  [<80053410>]  [<8003a694>]  [<80014368>]  [<8006026c>]  [<80011c84>]  [<80012508>]  [<80020b30>]  [<8006b238>]  [<8006b22c>]  [<c0029f74>]  [<c002bad4>]  [<c0030bc4>]  [<c002a0f0>]  [<c020d2b0>]  [<c002db88>]  [<c002d648>]  [<80012508>]  [<8004f970>]  [<800600ec>]  [<c0243540>]  [<8010156c>]  [<80101a0c>]  [<800ff4a8>]  [<80101b58>]  [<80101b44>]  [<800f7e54>]  [<800bc658>]  [<800f5e78>]  [<8007feb4>]  [<800f6734>]  [<8001a748>]  [<80099c50>] 

ADSL G.992 channel analysis

channel 1 selected 
device wl0 entered promiscuous mode

br0: port 2(wl0) entering learning state

br0: topology change detected, propagating

br0: port 2(wl0) entering forwarding state

device wl0.1 entered promiscuous mode

br0: port 3(wl0.1) entering learning state

br0: topology change detected, propagating

br0: port 3(wl0.1) entering forwarding state

udhcp server (v0.9.6) started
device eth0 left promiscuous mode

br0: port 1(eth0) entering disabled state

eth0.2: MAC Address: 11:22:33:44:55:66

eth0.3: MAC Address: 11:22:33:44:55:66

eth0.4: MAC Address: 11:22:33:44:55:66

eth0.5: MAC Address: 11:22:33:44:55:66

device wl0 left promiscuous mode

br0: port 2(wl0) entering disabled state

device wl0 entered promiscuous mode

br0: port 1(wl0) entering learning state

br0: topology change detected, propagating

br0: port 1(wl0) entering forwarding state

device wl0.1 left promiscuous mode

br0: port 3(wl0.1) entering disabled state

ADSL G.992 message exchange

device wl0.1 entered promiscuous mode

br0: port 2(wl0.1) entering learning state

br0: topology change detected, propagating

br0: port 2(wl0.1) entering forwarding state

device eth0.3 entered promiscuous mode

br0: port 3(eth0.3) entering learning state

br0: topology change detected, propagating

br0: port 3(eth0.3) entering forwarding state

device eth0.4 entered promiscuous mode

br0: port 4(eth0.4) entering learning state

br0: topology change detected, propagating

br0: port 4(eth0.4) entering forwarding state

device eth0.5 entered promiscuous mode

br0: port 5(eth0.5) entering learning state

br0: topology change detected, propagating

br0: port 5(eth0.5) entering forwarding state

ADSL link up, interleaved, us=796, ds=14668

device eth0.2 entered promiscuous mode

br0: port 6(eth0.2) entering learning state

br0: topology change detected, propagating

br0: port 6(eth0.2) entering forwarding state

eth0 Link DOWN.

Custom Firmware > Read Config
Read Flash: part=[BACKUP]

Enter kerSysBackupGet: strLen[24576],offset[0],fInfo.flash_backup_blk_offset=[0]

Backup[1-10]=[<psitree>

]

Read Flash: part=[PERSISTENT]

Custom Firmware > Find number of lines
eth0 Link UP.

Read Flash: part=[SCRATCH_PAD]

Read Flash: part=[SCRATCH_PAD]

Network initiated successfully!

Web Server initiated successfully!

No POSTINIT command need excuting
========>Load the wireless_reload_monitor thread<=======
sntp: host not found
Uninitialised timer!

This is just a warning.  Your computer is OK

function=0x00000000, data=0x0

Call Trace: [<8003e2ec>]  [<8003e330>]  [<c005805c>]  [<8004c170>]  [<8004c070>]  [<8003a694>]  [<801c29c0>]  [<8007feb4>]  [<8001a748>]  [<8001a748>]  [<80099c50>] 

BRCM NAT Caching disabled

--->syslog_options_up: blocksite loged!
--->syslog_options_up: webconnection logged!
Write Flash: part=[PERSISTENT]

Create remote upgrade thread successfully.
br0: port 6(eth0.2) entering disabled state

br0: port 3(eth0.3) entering disabled state

br0: port 4(eth0.4) entering disabled state

sending OFFER of 192.168.0.2
sending ACK to 192.168.0.2
Cfm initiated successfully!

Read Flash: part=[BACKUP]

Enter kerSysBackupGet: strLen[24576],offset[0],fInfo.flash_backup_blk_offset=[0]

Backup[1-10]=[<psitree>

]

b64[24]=[Sky PPP Password appears here]
PPP: PPP0_38_1 Start to connect ...
PPP: PPP0_38_1 Connection Up.
not find the appname [DDNSCfg]
not find the appname [Lan]
00:00:53 INFO:siproxd.c:187 siproxd-0.6.0-2836 mips-unknown-linux-gnu starting up
WAN monitor: interface ppp_0_38_1 is up. restart DDNS.
SNTP: Connection is established
zoucb: sntpStatus write OK.
clear port snooping failed: Address family not supported by protocol
RU - Send TTC request.
start connect ... fast2504.skyfirmware.com:80
RU - getTtcReq return: 0
Remote upgrade: server return TTC info:
RU - Fail to parse time_to_check RESPONSE.
Write Flash: part=[PERSISTENT]
```

# Auto-Update Log #

```
RU - Send TTC request.
start connect ... fast2504.skyfirmware.com:80
RU - getTtcReq return: 0
Remote upgrade: server return TTC info:

[SAGEM2504]
downloadHost=download.skyfirmware.com
firmwarePath=/?&mac=001122334455&firmwareVersion=2.8Sky&model=fast2504&product=skydsl
timetocheck=0
Write Flash: part=[PERSISTENT]

------------>go to run scheduled_in functions...
iptables: Bad rule (does a matching rule exist in that chain?)
start connect ... download.skyfirmware.com:80
iptables: Bad rule (does a matching rule exist in that chain?)
iptables: Bad rule (does a matching rule exist in that chain?)
iptables: Bad rule (does a matching rule exist in that chain?)
get fvc ret=0.
RU - fvc_url_path = download.skyfirmware.com/?&mac=001122334455&firmwareVersion=2.8Sky&model=fast2504&product=skydsl. ret = 1.
[sagem2504]=UNDEF
[sagem2504:downloadhost]=[download.skyfirmware.com]
[sagem2504:firmwarepath]=[/S/xxxxxxxxxx/xxxxxxxxxx/download/firmware/fast2504/]
[sagem2504:firmwareimage]=[2.8Sky]
[sagem2504:firmwaresize]=[3606649]
[sagem2504:configpath]=[/session/xxxxxxxxxx/xxxxxxxxxx/download/config/fast2504/]
[sagem2504:configname]=[FAST2504_standard_13.conf]
[sagem2504:statushost]=[download.skyfirmware.com]
[sagem2504:statuspath]=[/session/xxxxxxxxxx/xxxxxxxxxx/status/]
FVC file:
downloadhost:     [download.skyfirmware.com]
firmwarepath:     [/S/xxxxxxxxxx/xxxxxxxxxx/download/firmware/fast2504/]
firmwareimage:     [2.8Sky]
firmwaresize:     [3606649]
configpath:     [/session/xxxxxxxxxx/xxxxxxxxxx/download/config/fast2504/]
configname:     [FAST2504_standard_13.conf]
statushost:     [download.skyfirmware.com]
statuspath:     [/session/xxxxxxxxxx/xxxxxxxxxx/status/]
RU - post-url:[download.skyfirmware.com]
RU - post-path:[/session/xxxxxxxxxx/xxxxxxxxxx/status/]
RU - cfg_url_path = [http://download.skyfirmware.com/session/xxxxxxxxxx/xxxxxxxxxx/download/config/fast2504/FAST2504_standard_13.conf]
RU - fw_url_path=[http://download.skyfirmware.com/S/xxxxxxxxxxx/xxxxxxxxxxx/download/firmware/fast2504/2.8Sky]
RU - post_url_path=[http://download.skyfirmware.com/session/xxxxxxxxx/xxxxxxxxxxxxxxxx/status/]
running config version : [FAST2504_standard_13.conf]
remote config version : [FAST2504_standard_13.conf]
running sw_version: [2.8Sky]
remote fw_ver_file: [2.8Sky]
RU - firmware is up to date.
post url : [http://download.skyfirmware.com/session/xxxxxxxx/xxxxxxxxxxxx/status/]
Read Flash: part=[FACTORYINFO]

Enter kerSysFactoryInfoGet: strLen[1024],offset[0]

FactoryInfo[1-10]=[*********]

FactoryInfo=[*********]
start connect ... download.skyfirmware.com:80
Http POST OK
RU - config is up to date.
post url : [http://download.skyfirmware.com/session/xxxxxxxxxx/xxxxxxxxxx/status/]
Read Flash: part=[FACTORYINFO]

Enter kerSysFactoryInfoGet: strLen[1024],offset[0]

FactoryInfo[1-10]=[*********]

FactoryInfo=[************]
start connect ... download.skyfirmware.com:80
Http POST OK
Write Flash: part=[PERSISTENT]

Upgrade process finishes.
```