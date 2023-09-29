#!/bin/sh
set -e

# Create staff assets directory if not already present
if [ ! -d /home/wwwroot_staff ]; then
  mkdir -p /home/wwwroot_staff
fi

# Start SSH server
service ssh start

# Start the application
exec dotnet /app/Abbot.Web.dll "$@"
