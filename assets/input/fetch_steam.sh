#!/usr/bin/env bash

cd "$(dirname "$0")" || exit
wget https://api.steampowered.com/ISteamApps/GetAppList/v2/ -O steam.json