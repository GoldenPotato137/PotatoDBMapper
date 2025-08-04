#!/usr/bin/env bash

cd "$(dirname "$0")" || exit
mkdir bangumi
cd bangumi || exit

LATEST_JSON_URL="https://raw.githubusercontent.com/bangumi/Archive/master/aux/latest.json"
DOWNLOAD_URL=$(curl -s "$LATEST_JSON_URL" | jq -r '.browser_download_url')

echo "Downloading from $DOWNLOAD_URL ..."
wget "$DOWNLOAD_URL" -O bgm.zip

unzip bgm.zip
mv subject.jsonlines ..

cd .. || exit
rm -r bangumi
