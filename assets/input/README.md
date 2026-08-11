# Usage
The flowing data is required to run this project.

You can run bash in this folder to avoid downloading them manually.

## VNDB
Download it from https://dl.vndb.org/dump/vndb-db-latest.tar.zst

Unpack it and put everything in db folder here.

## Bangumi
Download it from https://github.com/bangumi/Archive/releases/tag/archive

Unpack it and put `subject.jsonlines` here.

## Steam
The old keyless `ISteamApps/GetAppList` endpoint is gone (it returns 404 now), so this needs a
[web api key](https://steamcommunity.com/dev/apikey). Put it in the `STEAM_API_KEY` environment
variable and run `fetch_steam.sh`, which pages through
[IStoreService/GetAppList](https://partner.steamgames.com/doc/webapi/IStoreService#GetAppList)
and saves the result as `steam.json` here (in the old `{"applist":{"apps":[...]}}` shape).

Without the key the steam mapping is simply skipped.

## 月幕Galgame (ymgal)
Download it from https://github.com/ymgal/data-export/releases/latest

Unpack it and put `game.jsonl` here, renamed to `ymgal_game.jsonl`.
