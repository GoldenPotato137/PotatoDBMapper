#!/usr/bin/env bash

# steam已经把不需要key的 ISteamApps/GetAppList 接口下掉了（现在直接返回404），
# 见 https://partner.steamgames.com/doc/webapi/ISteamApps ，
# 替代品是 IStoreService/GetAppList，需要一个web api key: https://steamcommunity.com/dev/apikey
# 把key放在环境变量STEAM_API_KEY里，github action里则是加一个同名的secret。

cd "$(dirname "$0")" || exit

if [ -z "$STEAM_API_KEY" ]; then
  echo "STEAM_API_KEY is not set, skip fetching steam app list."
  echo "Get one from https://steamcommunity.com/dev/apikey"
  exit 0
fi

# 一页最多50000个，每页的apps数组占一行，最后再拼成一个文件
: > steam_pages.json
last_appid=0
while : ; do
  curl -s -o steam_page.json "https://api.steampowered.com/IStoreService/GetAppList/v1/?key=$STEAM_API_KEY\
&include_games=true&max_results=50000&last_appid=$last_appid"
  if jq -e '.response.apps' steam_page.json > /dev/null 2>&1; then
    jq -c '.response.apps' steam_page.json >> steam_pages.json
    echo "fetched $(jq -r '.response.apps | length' steam_page.json) apps (last_appid=$last_appid) ..."
  else
    echo "Unexpected response from steam: $(head -c 200 steam_page.json)"
    rm -f steam_pages.json steam_page.json
    exit 0
  fi

  [ "$(jq -r '.response.have_more_results // false' steam_page.json)" = "true" ] || break
  last_appid=$(jq -r '.response.last_appid' steam_page.json)
done

# 拼成老的GetAppList格式，这样程序里不用为了换接口再改一遍
jq -c -s '{applist: {apps: [.[][] | {appid, name}]}}' steam_pages.json > steam.json
rm -f steam_pages.json steam_page.json
echo "steam.json: $(jq '.applist.apps | length' steam.json) apps"
